using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
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

    private static readonly Brush PositiveDelta = Frozen(0x88B300);
    private static readonly Brush NegativeDelta = Frozen(0xED1121);
    private static readonly Brush NeutralDelta = Frozen(0x8F9CA3);

    private static Brush Frozen(int rgb)
    {
        var brush = new SolidColorBrush(Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF)));
        brush.Freeze();
        return brush;
    }

    public MainViewModel()
    {
        client.ConnectionChanged += OnConnectionChanged;
        client.SnapshotReceived += OnSnapshotReceived;
    }

    public string DebugLogPath { get; } = Path.Combine(Path.GetTempPath(), "livecalculator-tosu.json");

    public void Start()
    {
        client.DebugLogPath = DebugLogPath;
        workerCts = new CancellationTokenSource();
        _ = Task.Run(() => ProcessLoop(workerCts.Token));
        client.Start();
    }

    public void Stop()
    {
        client.Stop();
        workerCts?.Cancel();
        signal.Set();
    }

    private void OnConnectionChanged(bool connected, string message)
    {
        Dispatch(() =>
        {
            IsConnected = connected;
            ConnectionStatus = message;
        });
    }

    private void OnSnapshotReceived(LiveSnapshot snapshot)
    {
        Interlocked.Exchange(ref pendingSnapshot, snapshot);
        signal.Set();
    }

    private void ProcessLoop(CancellationToken token)
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

                // In compact mode we only surface SR/delta/live-SR, so skip the pricier
                // per-frame PP computation (skills/graph are skipped in ApplyResult).
                var result = calculator.CalculateLive(snapshot, includePp: !compactMode);
                Dispatch(() => ApplyResult(snapshot, result));
            }
            catch (Exception ex)
            {
                Dispatch(() => ConnectionStatus = $"Calc error: {ex.Message}");
            }
        }
    }

    private void ApplyResult(LiveSnapshot s, LiveResult? result)
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
            CurrentStars = result.Stars;
            CurrentStarsText = result.Stars.ToString("0.00", CultureInfo.InvariantCulture);
            MaxStarsText = result.Stars.ToString("0.00", CultureInfo.InvariantCulture);
            PpText = result.Pp.ToString("0", CultureInfo.InvariantCulture);
            StarBrush = StarRatingColour.PillBrush(result.Stars);
            StarTextBrush = StarRatingColour.TextBrush(result.Stars);

            if (result.CurrentReady && result.CurrentStars.HasValue)
                LiveSrText = $"live {result.CurrentStars.Value.ToString("0.00", CultureInfo.InvariantCulture)}★";
            else
                LiveSrText = "live …";

            ApplyDelta(result.Stars, s.OfficialStars);

            // Skills breakdown + graph are hidden in compact mode, so don't spend time
            // rebuilding the legend or re-rendering the strain graph while minimized.
            if (!compactMode)
            {
                Diagnostics = $"Debug payload: {DebugLogPath}";
                UpdateSkills(result.Skills);
            }
        }
        else
        {
            CurrentStarsText = "—";
            MaxStarsText = "—";
            PpText = "—";
            LiveSrText = "";
            OfficialSrText = "—";
            DeltaText = "";
            ConclusionText = "";
            HasDelta = false;
            Diagnostics = calculator.Status;
            UpdateSkills(Array.Empty<SkillSeries>());
        }
    }

    private void ApplyDelta(double reworkStars, double officialStars)
    {
        if (officialStars <= 0)
        {
            HasDelta = false;
            OfficialSrText = "—";
            DeltaText = "";
            ConclusionText = "";
            return;
        }

        HasDelta = true;
        OfficialSrText = officialStars.ToString("0.00", CultureInfo.InvariantCulture);
        OldStarsText = officialStars.ToString("0.00", CultureInfo.InvariantCulture);
        OldStarBrush = StarRatingColour.PillBrush(officialStars);
        OldStarTextBrush = StarRatingColour.TextBrush(officialStars);

        double delta = reworkStars - officialStars;
        string sign = delta >= 0 ? "+" : "−";
        DeltaText = $"Δ {sign}{Math.Abs(delta).ToString("0.00", CultureInfo.InvariantCulture)}";

        if (delta >= 0.005)
        {
            DeltaBrush = PositiveDelta;
            ConclusionText = "Buffed";
            ConclusionBrush = PositiveDelta;
        }
        else if (delta <= -0.005)
        {
            DeltaBrush = NegativeDelta;
            ConclusionText = "Nerfed";
            ConclusionBrush = NegativeDelta;
        }
        else
        {
            DeltaBrush = NeutralDelta;
            ConclusionText = "≈ same";
            ConclusionBrush = NeutralDelta;
        }
    }

    private IReadOnlyList<SkillSeries>? lastSkills;

    private void UpdateSkills(IReadOnlyList<SkillSeries> skills)
    {
        lastSkills = skills;
        Skills = skills;

        SkillLegend.Clear();
        for (int i = 0; i < skills.Count; i++)
            SkillLegend.Add(new SkillLegendItem(skills[i].Name, SkillPalette.BrushForIndex(i), skills[i].Value));

        HasSkills = skills.Count > 0;
    }

    private static void Dispatch(Action action)
    {
        var app = Application.Current;
        if (app == null)
            return;

        if (app.Dispatcher.CheckAccess())
            action();
        else
            app.Dispatcher.BeginInvoke(action);
    }

    private bool compactMode;
    public bool CompactMode
    {
        get => compactMode;
        set
        {
            if (Set(ref compactMode, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullMode)));

                // Leaving compact mode: repopulate the skills breakdown immediately instead of
                // waiting for the next snapshot (which may not arrive at the menu / when paused).
                if (!compactMode && lastSkills != null)
                {
                    Diagnostics = $"Debug payload: {DebugLogPath}";
                    UpdateSkills(lastSkills);
                }
            }
        }
    }

    public bool FullMode => !compactMode;

    private bool isConnected;
    public bool IsConnected { get => isConnected; set => Set(ref isConnected, value); }

    private string connectionStatus = "Starting…";
    public string ConnectionStatus { get => connectionStatus; set => Set(ref connectionStatus, value); }

    private string title = "Waiting for osu!…";
    public string Title { get => title; set => Set(ref title, value); }

    private string artist = "";
    public string Artist { get => artist; set => Set(ref artist, value); }

    private string version = "";
    public string Version { get => version; set => Set(ref version, value); }

    private string mapper = "";
    public string Mapper { get => mapper; set => Set(ref mapper, value); }

    private string modsText = "NoMod";
    public string ModsText { get => modsText; set => Set(ref modsText, value); }

    private string stateText = "—";
    public string StateText { get => stateText; set => Set(ref stateText, value); }

    private string accuracyText = "100.00%";
    public string AccuracyText { get => accuracyText; set => Set(ref accuracyText, value); }

    private string comboText = "0x / 0x";
    public string ComboText { get => comboText; set => Set(ref comboText, value); }

    private double currentStars;
    public double CurrentStars { get => currentStars; set => Set(ref currentStars, value); }

    private string currentStarsText = "—";
    public string CurrentStarsText { get => currentStarsText; set => Set(ref currentStarsText, value); }

    private string maxStarsText = "—";
    public string MaxStarsText { get => maxStarsText; set => Set(ref maxStarsText, value); }

    private string liveSrText = "";
    public string LiveSrText { get => liveSrText; set => Set(ref liveSrText, value); }

    private string officialSrText = "—";
    public string OfficialSrText { get => officialSrText; set => Set(ref officialSrText, value); }

    private string deltaText = "";
    public string DeltaText { get => deltaText; set => Set(ref deltaText, value); }

    private Brush deltaBrush = NeutralDelta;
    public Brush DeltaBrush { get => deltaBrush; set => Set(ref deltaBrush, value); }

    private bool hasDelta;
    public bool HasDelta { get => hasDelta; set => Set(ref hasDelta, value); }

    private string oldStarsText = "—";
    public string OldStarsText { get => oldStarsText; set => Set(ref oldStarsText, value); }

    private Brush oldStarBrush = StarRatingColour.PillBrush(0);
    public Brush OldStarBrush { get => oldStarBrush; set => Set(ref oldStarBrush, value); }

    private Brush oldStarTextBrush = StarRatingColour.TextBrush(0);
    public Brush OldStarTextBrush { get => oldStarTextBrush; set => Set(ref oldStarTextBrush, value); }

    private string conclusionText = "";
    public string ConclusionText { get => conclusionText; set => Set(ref conclusionText, value); }

    private Brush conclusionBrush = NeutralDelta;
    public Brush ConclusionBrush { get => conclusionBrush; set => Set(ref conclusionBrush, value); }

    private string ppText = "—";
    public string PpText { get => ppText; set => Set(ref ppText, value); }

    private Brush starBrush = StarRatingColour.PillBrush(0);
    public Brush StarBrush { get => starBrush; set => Set(ref starBrush, value); }

    private Brush starTextBrush = StarRatingColour.TextBrush(0);
    public Brush StarTextBrush { get => starTextBrush; set => Set(ref starTextBrush, value); }

    private string diagnostics = "";
    public string Diagnostics { get => diagnostics; set => Set(ref diagnostics, value); }

    private IReadOnlyList<SkillSeries> skills = System.Array.Empty<SkillSeries>();
    public IReadOnlyList<SkillSeries> Skills { get => skills; set => Set(ref skills, value); }

    private bool hasSkills;
    public bool HasSkills { get => hasSkills; set => Set(ref hasSkills, value); }

    public ObservableCollection<SkillLegendItem> SkillLegend { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

public class SkillLegendItem
{
    public SkillLegendItem(string name, Brush brush, double value)
    {
        Name = name;
        Brush = brush;
        ValueText = value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    public string Name { get; }
    public Brush Brush { get; }
    public string ValueText { get; }
}
