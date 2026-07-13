using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using LiveCalculator.Osu;
using LiveCalculator.Tosu;

namespace LiveCalculator.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly TosuClient client = new();
    private readonly LiveDifficultyCalculator calculator = new();

    private readonly AutoResetEvent signal = new(false);
    private LiveSnapshot? pendingSnapshot;
    private CancellationTokenSource? workerCts;

    public MainViewModel()
    {
        client.ConnectionChanged += onConnectionChanged;
        client.SnapshotReceived += onSnapshotReceived;
    }

    public void Start()
    {
        workerCts = new CancellationTokenSource();
        _ = Task.Run(() => processLoop(workerCts.Token));
        client.Start();
    }

    public void Stop()
    {
        client.Stop();
        workerCts?.Cancel();
        signal.Set();
    }

    private void onConnectionChanged(bool connected, string message)
    {
        dispatch(() =>
        {
            IsConnected = connected;
            ConnectionStatus = message;
        });
    }

    private void onSnapshotReceived(LiveSnapshot snapshot)
    {
        // Coalesce bursts: keep only the newest snapshot and wake the worker.
        Interlocked.Exchange(ref pendingSnapshot, snapshot);
        signal.Set();
    }

    private void processLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            signal.WaitOne();

            var snapshot = Interlocked.Exchange(ref pendingSnapshot, null);
            if (snapshot == null || token.IsCancellationRequested)
                continue;

            try
            {
                if (calculator.PreparedKey != snapshot.MapKey)
                    calculator.Prepare(snapshot);

                var result = calculator.CalculateLive(snapshot);
                dispatch(() => applyResult(snapshot, result));
            }
            catch (Exception ex)
            {
                dispatch(() => ConnectionStatus = $"Calc error: {ex.Message}");
            }
        }
    }

    private void applyResult(LiveSnapshot s, LiveResult? result)
    {
        Title = string.IsNullOrEmpty(s.Title) ? "—" : s.Title;
        Artist = s.Artist;
        Version = string.IsNullOrEmpty(s.Version) ? "" : $"[{s.Version}]";
        Mapper = string.IsNullOrEmpty(s.Mapper) ? "" : $"mapped by {s.Mapper}";
        ModsText = s.Mods.Count == 0 ? "NoMod" : string.Join(" ", s.Mods);
        StateText = string.IsNullOrEmpty(s.StateName) ? "—" : s.StateName;
        AccuracyText = $"{s.Accuracy:0.00}%";
        ComboText = $"{s.CurrentCombo}x / {(result?.MaxCombo ?? 0)}x";

        if (result != null)
        {
            CurrentStars = result.CurrentStars;
            CurrentStarsText = result.CurrentStars.ToString("0.00", CultureInfo.InvariantCulture);
            MaxStarsText = result.MaxStars.ToString("0.00", CultureInfo.InvariantCulture);
            PpText = result.Pp.ToString("0", CultureInfo.InvariantCulture);
            StarBrush = new SolidColorBrush(StarRatingColour.ForStars(result.CurrentStars));
        }
        else
        {
            CurrentStarsText = "—";
            MaxStarsText = "—";
            PpText = "—";
        }
    }

    private static void dispatch(Action action)
    {
        var app = Application.Current;
        if (app == null)
            return;

        if (app.Dispatcher.CheckAccess())
            action();
        else
            app.Dispatcher.BeginInvoke(action);
    }

    #region Bound properties

    private bool isConnected;
    public bool IsConnected { get => isConnected; set => set(ref isConnected, value); }

    private string connectionStatus = "Starting…";
    public string ConnectionStatus { get => connectionStatus; set => set(ref connectionStatus, value); }

    private string title = "Waiting for osu!…";
    public string Title { get => title; set => set(ref title, value); }

    private string artist = "";
    public string Artist { get => artist; set => set(ref artist, value); }

    private string version = "";
    public string Version { get => version; set => set(ref version, value); }

    private string mapper = "";
    public string Mapper { get => mapper; set => set(ref mapper, value); }

    private string modsText = "NoMod";
    public string ModsText { get => modsText; set => set(ref modsText, value); }

    private string stateText = "—";
    public string StateText { get => stateText; set => set(ref stateText, value); }

    private string accuracyText = "100.00%";
    public string AccuracyText { get => accuracyText; set => set(ref accuracyText, value); }

    private string comboText = "0x / 0x";
    public string ComboText { get => comboText; set => set(ref comboText, value); }

    private double currentStars;
    public double CurrentStars { get => currentStars; set => set(ref currentStars, value); }

    private string currentStarsText = "—";
    public string CurrentStarsText { get => currentStarsText; set => set(ref currentStarsText, value); }

    private string maxStarsText = "—";
    public string MaxStarsText { get => maxStarsText; set => set(ref maxStarsText, value); }

    private string ppText = "—";
    public string PpText { get => ppText; set => set(ref ppText, value); }

    private Brush starBrush = new SolidColorBrush(StarRatingColour.ForStars(0));
    public Brush StarBrush { get => starBrush; set => set(ref starBrush, value); }

    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;

    private void set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
