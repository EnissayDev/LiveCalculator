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
    private readonly TosuClient _client = new();
    private readonly LiveDifficultyCalculator _calculator = new();

    private readonly AutoResetEvent _signal = new(false);
    private LiveSnapshot? _pendingSnapshot;
    private CancellationTokenSource? _workerCts;

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
        _client.ConnectionChanged += OnConnectionChanged;
        _client.SnapshotReceived += OnSnapshotReceived;
    }

    public string DebugLogPath { get; } = Path.Combine(Path.GetTempPath(), "livecalculator-tosu.json");

    public void Start()
    {
        _client.DebugLogPath = DebugLogPath;
        _workerCts = new CancellationTokenSource();
        _ = Task.Run(() => ProcessLoop(_workerCts.Token));
        _client.Start();
    }

    public void Stop()
    {
        _client.Stop();
        _workerCts?.Cancel();
        _signal.Set();
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
        Interlocked.Exchange(ref _pendingSnapshot, snapshot);
        _signal.Set();
    }

    private void ProcessLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _signal.WaitOne();

            var snapshot = Interlocked.Exchange(ref _pendingSnapshot, null);
            if (snapshot == null || token.IsCancellationRequested)
                continue;

            try
            {
                if (_calculator.PreparedKey != snapshot.MapKey)
                    _calculator.Prepare(snapshot);

                // In compact mode we only surface SR/delta/live-SR, so skip the pricier
                // per-frame PP computation (skills/graph are skipped in ApplyResult).
                var result = _calculator.CalculateLive(snapshot, includePp: !_compactMode);
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
            if (!_compactMode)
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
            Diagnostics = _calculator.Status;
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

    private IReadOnlyList<SkillSeries>? _lastSkills;

    private void UpdateSkills(IReadOnlyList<SkillSeries> skills)
    {
        _lastSkills = skills;
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

    private bool _compactMode;
    public bool CompactMode
    {
        get => _compactMode;
        set
        {
            if (Set(ref _compactMode, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullMode)));

                // Leaving compact mode: repopulate the skills breakdown immediately instead of
                // waiting for the next snapshot (which may not arrive at the menu / when paused).
                if (!_compactMode && _lastSkills != null)
                {
                    Diagnostics = $"Debug payload: {DebugLogPath}";
                    UpdateSkills(_lastSkills);
                }
            }
        }
    }

    public bool FullMode => !_compactMode;

    private bool _isConnected;
    public bool IsConnected { get => _isConnected; set => Set(ref _isConnected, value); }

    private string _connectionStatus = "Starting…";
    public string ConnectionStatus { get => _connectionStatus; set => Set(ref _connectionStatus, value); }

    private string _title = "Waiting for osu!…";
    public string Title { get => _title; set => Set(ref _title, value); }

    private string _artist = "";
    public string Artist { get => _artist; set => Set(ref _artist, value); }

    private string _version = "";
    public string Version { get => _version; set => Set(ref _version, value); }

    private string _mapper = "";
    public string Mapper { get => _mapper; set => Set(ref _mapper, value); }

    private string _modsText = "NoMod";
    public string ModsText { get => _modsText; set => Set(ref _modsText, value); }

    private string _stateText = "—";
    public string StateText { get => _stateText; set => Set(ref _stateText, value); }

    private string _accuracyText = "100.00%";
    public string AccuracyText { get => _accuracyText; set => Set(ref _accuracyText, value); }

    private string _comboText = "0x / 0x";
    public string ComboText { get => _comboText; set => Set(ref _comboText, value); }

    private double _currentStars;
    public double CurrentStars { get => _currentStars; set => Set(ref _currentStars, value); }

    private string _currentStarsText = "—";
    public string CurrentStarsText { get => _currentStarsText; set => Set(ref _currentStarsText, value); }

    private string _maxStarsText = "—";
    public string MaxStarsText { get => _maxStarsText; set => Set(ref _maxStarsText, value); }

    private string _liveSrText = "";
    public string LiveSrText { get => _liveSrText; set => Set(ref _liveSrText, value); }

    private string _officialSrText = "—";
    public string OfficialSrText { get => _officialSrText; set => Set(ref _officialSrText, value); }

    private string _deltaText = "";
    public string DeltaText { get => _deltaText; set => Set(ref _deltaText, value); }

    private Brush _deltaBrush = NeutralDelta;
    public Brush DeltaBrush { get => _deltaBrush; set => Set(ref _deltaBrush, value); }

    private bool _hasDelta;
    public bool HasDelta { get => _hasDelta; set => Set(ref _hasDelta, value); }

    private string _oldStarsText = "—";
    public string OldStarsText { get => _oldStarsText; set => Set(ref _oldStarsText, value); }

    private Brush _oldStarBrush = StarRatingColour.PillBrush(0);
    public Brush OldStarBrush { get => _oldStarBrush; set => Set(ref _oldStarBrush, value); }

    private Brush _oldStarTextBrush = StarRatingColour.TextBrush(0);
    public Brush OldStarTextBrush { get => _oldStarTextBrush; set => Set(ref _oldStarTextBrush, value); }

    private string _conclusionText = "";
    public string ConclusionText { get => _conclusionText; set => Set(ref _conclusionText, value); }

    private Brush _conclusionBrush = NeutralDelta;
    public Brush ConclusionBrush { get => _conclusionBrush; set => Set(ref _conclusionBrush, value); }

    private string _ppText = "—";
    public string PpText { get => _ppText; set => Set(ref _ppText, value); }

    private Brush _starBrush = StarRatingColour.PillBrush(0);
    public Brush StarBrush { get => _starBrush; set => Set(ref _starBrush, value); }

    private Brush _starTextBrush = StarRatingColour.TextBrush(0);
    public Brush StarTextBrush { get => _starTextBrush; set => Set(ref _starTextBrush, value); }

    private string _diagnostics = "";
    public string Diagnostics { get => _diagnostics; set => Set(ref _diagnostics, value); }

    private IReadOnlyList<SkillSeries> _skills = System.Array.Empty<SkillSeries>();
    public IReadOnlyList<SkillSeries> Skills { get => _skills; set => Set(ref _skills, value); }

    private bool _hasSkills;
    public bool HasSkills { get => _hasSkills; set => Set(ref _hasSkills, value); }

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
