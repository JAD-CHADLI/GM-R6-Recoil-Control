using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using System.Reflection; // for SetDoubleBuffered
using System.Drawing.Drawing2D;

namespace MouseSliderApp
{
    public class Form1 : Form
    {
        // ====== Pages ======
        private Panel _pageProfiles = null!;
        private Panel _pageSettings = null!;
        private Panel _pageAbout = null!;
        private Panel _pageTutorial = null!;

        // Container + custom scrollbar for profiles
        private Panel _profilesContainer = null!;
        private Panel _profilesScrollTrack = null!;   // fake scrollbar track
        private Panel _profilesScrollThumb = null!;   // fake scrollbar thumb
        private int _profilesScrollOffset = 0;
        private int _profilesMaxScrollOffset = 0;Updates
        private const int ProfilesScrollStep = 40;

        // ====== Movement controls (Settings page) ======
        private Label _labelHorizontal = null!;
        private TrackBar _trackBarHorizontal = null!;
        private TextBox _textHorizontal = null!;

        private Label _labelVertical = null!;
        private TrackBar _trackBarVertical = null!;
        private TextBox _textVertical = null!;

        // Global buttons on profiles page
        private Button _buttonStart = null!;
        private Button _buttonResetAll = null!;   // now only used in Tutorial header
        private Button _buttonAbout = null!;
        private Button _buttonExportSettings = null!;
        private Button _buttonImportSettings = null!;
        private Button _buttonTutorial = null!;

        // movement + setup "cards"
        private Panel _movementCard = null!;
        private Panel _setupCard = null!;

        // ====== Weapon UI (Settings page) ======
        private Panel _weaponCard = null!;
        private Button _buttonPrimaryPrev = null!;
        private Button _buttonPrimaryNext = null!;
        private Button _buttonSecondaryPrev = null!;
        private Button _buttonSecondaryNext = null!;
        private Label _labelPrimaryWeaponName = null!;
        private Label _labelSecondaryWeaponName = null!;
        private PictureBox _picturePrimaryWeapon = null!;
        private PictureBox _pictureSecondaryWeapon = null!;

        // ====== Category / Profile selection (Profiles page) ======
        private Label _labelCategory = null!; // (no longer used but kept for compatibility if needed)
        private Button _buttonCategoryA = null!;
        private Button _buttonCategoryB = null!;
        private TextBox _searchBox = null!;
        private Label _labelSelectedProfile = null!;
        private Label _labelSelectedSetup = null!;
        private FlowLayoutPanel _profilesPanel = null!;
        private Panel? _selectedProfileCard;
        private Panel _profilesTopBar = null!;

        // search placeholder state
        private bool _searchHasPlaceholder = true;
        private const string SearchPlaceholder = "Search...";

        // ====== Setups + Keybinds (Settings page) ======
        private Label _labelActiveSetup = null!;
        private Label _labelSetup1 = null!;
        private TextBox _textKey1 = null!;
        private Button _buttonSetKey1 = null!;
        private Button _buttonSaveSetup1 = null!;

        private Label _labelSetup2 = null!;
        private TextBox _textKey2 = null!;
        private Button _buttonSetKey2 = null!;
        private Button _buttonSaveSetup2 = null!;

        // summary labels that show saved H/V for each setup
        private Label _labelSetup1Summary = null!;
        private Label _labelSetup2Summary = null!;

        // NEW: Setup 3 (Maestro only)
        private Label _labelSetup3 = null!;
        private TextBox _textKey3 = null!;
        private Button _buttonSetKey3 = null!;
        private Button _buttonSaveSetup3 = null!;
        private Label _labelSetup3Summary = null!;

        // NEW: key state for Setup 3
        private bool _key3WasDown;

        // Global Start/Stop hotkey (stored in a hidden "global" profile)
        private Profile? _globalProfile;
        private TextBox _textToggleKey = null!;
        private Button _buttonSetToggleKey = null!;
        private bool _capturingToggleKey;
        private bool _toggleKeyWasDown;



        // ====== Profile image (Settings page) ======
        private PictureBox _pictureProfile = null!;
        private Label _labelEditingProfile = null!;
        private Button _buttonBack = null!;
        private Button _buttonResetProfile = null!;   // per-profile reset

        // Settings watermark logo
        private PictureBox _settingsLogoWatermark = null!;

        // ====== Movement state ======
        private bool _isActive;
        private bool _comboArmed;
        private bool _comboActive;

        private System.Windows.Forms.Timer _movementTimer = null!;

        private double _horizontalSpeed;
        private double _verticalSpeed;
        private double _accumulatedX;
        private double _accumulatedY;

        private const double SliderScale = 100.0;
        private const int MovementTimerIntervalMs = 20;
        // NEW
        private const double HorizontalStrengthMultiplier = 1.0;  // 1.0 = unchanged
        private const double VerticalStrengthMultiplier = 2;  // 1.5x stronger vertical, for example

        private const int HorizontalTrackBarMin = -10000;
        private const int HorizontalTrackBarMax = 10000;
        private const int VerticalTrackBarMin = 0;
        private const int VerticalTrackBarMax = 10000;

        // Card colors (for hover/selection)
        private static readonly Color CardNormalColor = Color.FromArgb(30, 41, 59);
        private static readonly Color CardHoverColor = Color.FromArgb(51, 65, 85);
        private static readonly Color CardSelectedColor = Color.FromArgb(37, 99, 235);

        // Theme colors
        private static readonly Color BgMain = Color.FromArgb(10, 12, 24);
        private static readonly Color BgHeader = Color.FromArgb(15, 23, 42);
        private static readonly Color BgTopBar = Color.FromArgb(17, 24, 39);
        private static readonly Color BgSettings = Color.FromArgb(15, 23, 42);

        private static readonly Color AccentPrimary = Color.FromArgb(59, 130, 246);   // primary blue
        private static readonly Color AccentPrimarySoft = Color.FromArgb(37, 99, 235);
        private static readonly Color AccentPositive = Color.FromArgb(34, 197, 94);   // green
        private static readonly Color AccentDanger = Color.FromArgb(239, 68, 68);     // red
        private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

        private const string ActiveBadgeName = "ActiveBadge";

        // App logo (shared) – used in headers + watermark
        private Image? _appLogoImage;

        // Tooltips
        private ToolTip _toolTip = null!;

        // ====== Profile model ======
        private class Profile
        {
            public string Category { get; }
            public int Index { get; }
            public string Name { get; }
            public string? ImageFileName { get; }

            // Old per-setup values (kept for save compatibility / fallback)
            public double Horizontal1 { get; set; }
            public double Vertical1 { get; set; }
            public Keys Key1 { get; set; } = Keys.None;

            public double Horizontal2 { get; set; }
            public double Vertical2 { get; set; }
            public Keys Key2 { get; set; } = Keys.None;

            // NEW: Setup 3 (used only for Maestro)
            public double Horizontal3 { get; set; }
            public double Vertical3 { get; set; }
            public Keys Key3 { get; set; } = Keys.None;


            // New: weapon lists
            public List<WeaponInfo> PrimaryWeapons { get; } = new();
            public List<WeaponInfo> SecondaryWeapons { get; } = new();

            public int SelectedPrimaryIndex { get; set; }
            public int SelectedSecondaryIndex { get; set; }

            public WeaponInfo? SelectedPrimaryWeapon =>
                (SelectedPrimaryIndex >= 0 && SelectedPrimaryIndex < PrimaryWeapons.Count)
                    ? PrimaryWeapons[SelectedPrimaryIndex]
                    : null;

            public WeaponInfo? SelectedSecondaryWeapon =>
                (SelectedSecondaryIndex >= 0 && SelectedSecondaryIndex < SecondaryWeapons.Count)
                    ? SecondaryWeapons[SelectedSecondaryIndex]
                    : null;

            public Profile(string category, int index, string name, string? imageFileName)
            {
                Category = category;
                Index = index;
                Name = name;
                ImageFileName = imageFileName;

                Horizontal1 = 0.0;
                Vertical1 = 0.0;
                Horizontal2 = 0.0;
                Vertical2 = 0.0;

                // NEW
                Horizontal3 = 0.0;
                Vertical3 = 0.0;


                SelectedPrimaryIndex = 0;
                SelectedSecondaryIndex = 0;
            }

            public override string ToString() => Name;
        }

        // New: weapon recoil model
        private class WeaponInfo
        {
            public string Name { get; }
            public string? ImageFileName { get; }

            public double Horizontal { get; set; }
            public double Vertical { get; set; }

            public WeaponInfo(string name, string? imageFileName)
            {
                Name = name;
                ImageFileName = imageFileName;
                Horizontal = 0.0;
                Vertical = 0.0;
            }

            public override string ToString() => Name;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryEnableDarkTitleBar();
        }

        private class WeaponData
        {
            // "Primary" or "Secondary"
            public string Slot { get; set; } = "";

            // Index in the PrimaryWeapons / SecondaryWeapons list
            public int Index { get; set; }

            public double Horizontal { get; set; }
            public double Vertical { get; set; }
        }

        private class ProfileData
        {
            public string Category { get; set; } = "";
            public int Index { get; set; }

            // old (still used)
            public double Horizontal1 { get; set; }
            public double Vertical1 { get; set; }
            public double Horizontal2 { get; set; }
            public double Vertical2 { get; set; }
            public Keys Key1 { get; set; }
            public Keys Key2 { get; set; }

            // NEW: Setup 3
            public double Horizontal3 { get; set; }
            public double Vertical3 { get; set; }
            public Keys Key3 { get; set; }


            // NEW: which weapons are selected
            public int SelectedPrimaryIndex { get; set; }
            public int SelectedSecondaryIndex { get; set; }

            // NEW: all weapon recoil values
            public List<WeaponData> Weapons { get; set; } = new();
        }


        private readonly List<Profile> _profiles = new();
        private readonly Dictionary<Profile, Panel> _profileCardCache = new(); // cache cards

        private string _currentCategory = "A";
        private string _currentSearchText = string.Empty;
        private Profile? _currentProfile;

        private int _currentSetupIndex = 1;
        private int _capturingKeyForSetup = 0;
        private bool _key1WasDown;
        private bool _key2WasDown;

        private readonly string _dataFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles.json");
        private readonly string _imagesFolder =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(Keys vKey);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_MOVE = 0x0001;

        public Form1()
        {
            KeyPreview = true;
            InitializeUi();

            // High-res icon for the window (Windows will still draw it small in the title bar)
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    Icon = new Icon(iconPath);
                }
            }
            catch
            {
                // ignore if something goes wrong
            }

            // existing stuff
            SetDoubleBuffered(this);
            SetDoubleBuffered(_pageProfiles);
            SetDoubleBuffered(_pageSettings);
            SetDoubleBuffered(_profilesPanel);

            CreateProfiles();
            InitializeWeapons();      // new
            LoadProfilesFromFile();
            SyncToggleKeyUi();
            ShowCategory("A");
            ShowProfilesPage();
        }

        // For dark title bar / border
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;


        private void UpdateSetup3Visibility()
        {
            bool isMaestro = _currentProfile != null &&
                             string.Equals(_currentProfile.Name, "Maestro", StringComparison.OrdinalIgnoreCase);

            if (_labelSetup3 != null) _labelSetup3.Visible = isMaestro;
            if (_textKey3 != null) _textKey3.Visible = isMaestro;
            if (_buttonSetKey3 != null) _buttonSetKey3.Visible = isMaestro;
            if (_buttonSaveSetup3 != null) _buttonSaveSetup3.Visible = isMaestro;
            if (_labelSetup3Summary != null) _labelSetup3Summary.Visible = isMaestro;
        }

        private void TryEnableDarkTitleBar()
        {
            try
            {
                int useDark = 1;
                // Ask Windows to use the dark title bar / border for this window
                DwmSetWindowAttribute(
                    Handle,
                    DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref useDark,
                    sizeof(int));
            }
            catch
            {
                // If it fails (older Windows), just ignore – app still works
            }
        }

        // ==========================================================
        // UI setup
        // ==========================================================
        private void InitializeUi()
        {
            Text = "GM R6 Recoil Control";
            ClientSize = new Size(1100, 650);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = BgMain;
            ForeColor = Color.White;

            KeyDown += Form1_KeyDown;
            FormClosing += Form1_FormClosing;

            _toolTip = CreateDefaultToolTip();

            _movementTimer = new System.Windows.Forms.Timer { Interval = MovementTimerIntervalMs };
            _movementTimer.Tick += MovementTimer_Tick;
            _movementTimer.Start();

            _pageProfiles = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgMain
            };

            _pageSettings = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgMain,
                Visible = false
            };

            _pageAbout = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgMain,
                Visible = false
            };

            _pageTutorial = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgMain,
                Visible = false
            };

            Controls.Add(_pageTutorial);
            Controls.Add(_pageAbout);
            Controls.Add(_pageSettings);
            Controls.Add(_pageProfiles);

            // load logo once
            LoadAppLogo();

            BuildProfilesPage();
            BuildSettingsPage();
            BuildAboutPage();
            BuildTutorialPage();
        }

        private ToolTip CreateDefaultToolTip()
        {
            return new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 400,
                ReshowDelay = 200,
                ShowAlways = true
            };
        }

        private void LoadAppLogo()
        {
            // Put your logo at /Images/AppLogo.png
            try
            {
                string logoPath = Path.Combine(_imagesFolder, "AppLogo.png");
                if (!File.Exists(logoPath))
                    return;

                using (var img = Image.FromFile(logoPath))
                {
                    _appLogoImage = new Bitmap(img);
                }
            }
            catch
            {
                // ignore – stays null
            }
        }

        // ===== Profiles page (main menu) =====
        private void BuildProfilesPage()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = BgHeader
            };

            // BIGGER logo in header
            var logoBox = new PictureBox
            {
                Size = new Size(40, 40),
                Location = new Point(15, 10),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            if (_appLogoImage != null)
            {
                logoBox.Image = _appLogoImage;
            }
            else
            {
                logoBox.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(Color.FromArgb(55, 65, 81), 2))
                    {
                        e.Graphics.DrawRectangle(pen, 3, 3, logoBox.Width - 6, logoBox.Height - 6);
                    }
                };
            }

            var titleLabel = new Label
            {
                AutoSize = true,
                Text = "GM R6 Recoil Control",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(logoBox.Right + 12, 14)
            };

            _buttonStart = new Button
            {
                Text = "Start",
                Width = 90,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentPrimary,
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top
            };
            _buttonStart.FlatAppearance.BorderSize = 0;
            _buttonStart.Click += ButtonStart_Click;
            ApplyRoundedCorners(_buttonStart, 6);

            _buttonAbout = CreateFlatButton("About", 80, 30);
            _buttonTutorial = CreateFlatButton("Tutorial", 80, 30);

            _buttonAbout.Click += (s, e) => ShowAboutPage();
            _buttonTutorial.Click += (s, e) => ShowTutorialPage();

            header.Controls.Add(logoBox);
            header.Controls.Add(titleLabel);
            header.Controls.Add(_buttonStart);
            header.Controls.Add(_buttonAbout);
            header.Controls.Add(_buttonTutorial);

            // Tooltips for main header
            _toolTip.SetToolTip(_buttonStart, "Toggle mouse movement for the selected profile.");
            _toolTip.SetToolTip(_buttonAbout, "Show information about the application.");
            _toolTip.SetToolTip(_buttonTutorial, "Open the quick tutorial.");

            // Layout: Start centered, About + Tutorial on right (no Reset All here)
            header.Resize += (s, e) =>
            {
                int top = 15;
                int marginRight = 20;
                int gap = 10;

                // Right side: Tutorial then About
                int xRight = header.Width - marginRight;

                if (_buttonTutorial != null)
                {
                    _buttonTutorial.Location = new Point(
                        xRight - _buttonTutorial.Width,
                        top);
                    xRight = _buttonTutorial.Left - gap;
                }

                if (_buttonAbout != null)
                {
                    _buttonAbout.Location = new Point(
                        xRight - _buttonAbout.Width,
                        top);
                }

                // Center: Start button alone
                if (_buttonStart != null)
                {
                    int centerX = header.Width / 2;
                    int startLeft = centerX - _buttonStart.Width / 2;
                    if (startLeft < 0) startLeft = 0;

                    _buttonStart.Location = new Point(startLeft, top);
                }
            };

            header.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(31, 41, 55)))
                {
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
                }
            };

            // top bar with category + selected label + search
            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = BgTopBar,
                Padding = new Padding(15, 10, 15, 5)
            };
            _profilesTopBar = topBar;

            topBar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(31, 41, 55)))
                {
                    e.Graphics.DrawLine(pen, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
                }
            };

            _buttonCategoryA = CreateFlatButton("Attackers", 100, 28);
            _buttonCategoryB = CreateFlatButton("Defenders", 100, 28);

            StyleSegmentButton(_buttonCategoryA, true);
            StyleSegmentButton(_buttonCategoryB, false);

            _buttonCategoryA.Click += (s, e) => ShowCategory("A");
            _buttonCategoryB.Click += (s, e) => ShowCategory("B");

            _toolTip.SetToolTip(_buttonCategoryA, "Show attacker operators.");
            _toolTip.SetToolTip(_buttonCategoryB, "Show defender operators.");

            // search box with placeholder
            _searchBox = new TextBox
            {
                Width = 170,
                Height = 24,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = TextMuted,
                Text = SearchPlaceholder
            };
            _searchHasPlaceholder = true;
            _searchBox.GotFocus += SearchBox_GotFocus;
            _searchBox.LostFocus += SearchBox_LostFocus;
            _searchBox.TextChanged += SearchBox_TextChanged;
            _toolTip.SetToolTip(_searchBox, "Search operators by name.");

            _labelSelectedProfile = new Label
            {
                AutoSize = true,
                Text = "Selected profile: (none)",
                ForeColor = TextMuted
            };

            _labelSelectedSetup = new Label
            {
                AutoSize = true,
                Text = "Setup: (none)",
                ForeColor = TextMuted
            };

            topBar.Controls.Add(_buttonCategoryA);
            topBar.Controls.Add(_buttonCategoryB);
            topBar.Controls.Add(_searchBox);
            topBar.Controls.Add(_labelSelectedProfile);
            topBar.Controls.Add(_labelSelectedSetup);

            topBar.Resize += (s, e) => LayoutProfilesTopBar();
            LayoutProfilesTopBar();

            // profile cards container + FAKE custom scrollbar
            _profilesContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgMain
            };

            _profilesScrollTrack = new Panel
            {
                Dock = DockStyle.Right,
                Width = 6,
                BackColor = BgHeader
            };

            _profilesScrollThumb = new Panel
            {
                Width = _profilesScrollTrack.Width,
                Height = 40,
                BackColor = AccentPrimarySoft,
                Visible = false
            };
            ApplyRoundedCorners(_profilesScrollThumb, 3);
            _profilesScrollTrack.Controls.Add(_profilesScrollThumb);

            _profilesPanel = new FlowLayoutPanel
            {
                AutoScroll = false,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = BgMain,
                Location = new Point(0, 0),
                Padding = new Padding(0, 20, 0, 20)
            };
            _profilesPanel.Resize += ProfilesPanel_Resize;
            _profilesPanel.ControlAdded += ProfilesPanel_ControlChanged;
            _profilesPanel.ControlRemoved += ProfilesPanel_ControlChanged;
            _profilesPanel.MouseWheel += ProfilesPanel_MouseWheel;

            _profilesContainer.Controls.Add(_profilesPanel);
            _profilesContainer.Controls.Add(_profilesScrollTrack);
            _profilesContainer.Resize += ProfilesContainer_Resize;

            _pageProfiles.Controls.Add(_profilesContainer);
            _pageProfiles.Controls.Add(topBar);
            _pageProfiles.Controls.Add(header);
        }

        // Lay out the top bar whenever size or text changes
        private void LayoutProfilesTopBar()
        {
            if (_profilesTopBar == null)
                return;

            int gap = 10;
            int y = 10;
            int rightMargin = 20;

            // left: search box
            if (_searchBox != null)
            {
                _searchBox.Location = new Point(15, y + 3);
            }

            // center: Attackers / Defenders ONLY (no "Side" text)
            if (_buttonCategoryA == null || _buttonCategoryB == null)
                return;

            int totalWidth = _buttonCategoryA.Width + gap + _buttonCategoryB.Width;
            int startX = (_profilesTopBar.ClientSize.Width - totalWidth) / 2;
            if (startX < 10) startX = 10;

            _buttonCategoryA.Location = new Point(startX, y);
            _buttonCategoryB.Location = new Point(_buttonCategoryA.Right + gap, y);

            // right: selected profile + setup
            if (_labelSelectedProfile == null || _labelSelectedSetup == null)
                return;

            int rightX = _profilesTopBar.ClientSize.Width - rightMargin;

            _labelSelectedProfile.Location = new Point(
                rightX - _labelSelectedProfile.Width,
                y + 0);

            _labelSelectedSetup.Location = new Point(
                rightX - _labelSelectedSetup.Width,
                _labelSelectedProfile.Bottom + 2);
        }

        // center profiles + sync custom scrollbar
        private void ProfilesPanel_Resize(object? sender, EventArgs e)
        {
            if (_profilesContainer != null && _profilesScrollTrack != null)
            {
                _profilesPanel.Width = _profilesContainer.ClientSize.Width - _profilesScrollTrack.Width;
            }

            CenterProfiles();
            UpdateProfilesScrollBar();
        }

        private void ProfilesContainer_Resize(object? sender, EventArgs e)
        {
            if (_profilesPanel != null && _profilesScrollTrack != null)
            {
                _profilesPanel.Width = _profilesContainer.ClientSize.Width - _profilesScrollTrack.Width;
            }
            CenterProfiles();
            UpdateProfilesScrollBar();
        }

        private void ProfilesPanel_ControlChanged(object? sender, ControlEventArgs e)
        {
            CenterProfiles();
            UpdateProfilesScrollBar();
        }

        private void CenterProfiles()
        {
            if (_profilesPanel.Controls.Count == 0)
            {
                _profilesPanel.Padding = new Padding(0, 20, 0, 20);
                return;
            }

            int panelWidth = _profilesPanel.ClientSize.Width;
            if (panelWidth <= 0) return;

            var first = _profilesPanel.Controls[0];
            int itemWidth = first.Width + first.Margin.Horizontal;
            if (itemWidth <= 0) return;

            int columns = panelWidth / itemWidth;
            if (columns < 1) columns = 1;

            int usedWidth = columns * itemWidth;
            int extra = panelWidth - usedWidth;
            if (extra < 0) extra = 0;

            int leftPad = extra / 2;
            _profilesPanel.Padding = new Padding(leftPad, 20, 0, 20);
        }

        private void UpdateProfilesScrollBar()
        {
            if (_profilesContainer == null || _profilesPanel == null)
                return;

            _profilesPanel.PerformLayout();

            int contentHeight = 0;
            foreach (Control c in _profilesPanel.Controls)
            {
                if (c.Bottom > contentHeight)
                    contentHeight = c.Bottom;
            }

            int viewportHeight = _profilesContainer.ClientSize.Height;
            if (viewportHeight <= 0) viewportHeight = 1;

            int totalContentHeight = contentHeight + _profilesPanel.Padding.Vertical;
            _profilesPanel.Height = Math.Max(_profilesContainer.ClientSize.Height, totalContentHeight);

            int maxOffset = Math.Max(0, totalContentHeight - viewportHeight);
            _profilesMaxScrollOffset = maxOffset;

            if (_profilesScrollOffset > maxOffset)
                _profilesScrollOffset = maxOffset;
            if (_profilesScrollOffset < 0)
                _profilesScrollOffset = 0;

            bool scrollable = maxOffset > 0;

            if (_profilesScrollTrack != null && _profilesScrollThumb != null)
            {
                if (!scrollable)
                {
                    _profilesScrollThumb.Visible = false;
                }
                else
                {
                    _profilesScrollThumb.Visible = true;

                    int trackHeight = _profilesScrollTrack.ClientSize.Height;
                    if (trackHeight <= 0) trackHeight = 1;

                    double viewportRatio = viewportHeight / (double)totalContentHeight;
                    if (viewportRatio > 1.0) viewportRatio = 1.0;
                    if (viewportRatio < 0.1) viewportRatio = 0.1;

                    int thumbHeight = Math.Max(20, (int)(trackHeight * viewportRatio));
                    if (thumbHeight > trackHeight) thumbHeight = trackHeight;
                    _profilesScrollThumb.Height = thumbHeight;

                    double scrollRatio = maxOffset == 0 ? 0.0 : _profilesScrollOffset / (double)maxOffset;
                    int thumbMaxTravel = trackHeight - thumbHeight;
                    if (thumbMaxTravel < 0) thumbMaxTravel = 0;

                    int thumbTop = (int)(thumbMaxTravel * scrollRatio);
                    if (thumbTop < 0) thumbTop = 0;
                    if (thumbTop > thumbMaxTravel) thumbTop = thumbMaxTravel;

                    _profilesScrollThumb.Top = thumbTop;
                }
            }

            _profilesPanel.Location = new Point(0, -_profilesScrollOffset);
        }

        private void ProfilesPanel_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (_profilesMaxScrollOffset <= 0) return;

            int delta = -e.Delta;
            int step = ProfilesScrollStep;
            int newOffset = _profilesScrollOffset + (delta > 0 ? step : -step);

            if (newOffset < 0)
                newOffset = 0;
            if (newOffset > _profilesMaxScrollOffset)
                newOffset = _profilesMaxScrollOffset;

            _profilesScrollOffset = newOffset;
            UpdateProfilesScrollBar();
        }

        // ===== Settings page =====
        private void BuildSettingsPage()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = BgHeader
            };

            _buttonBack = new Button
            {
                Text = "← Back",
                Width = 80,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                Location = new Point(15, 15)
            };
            _buttonBack.FlatAppearance.BorderSize = 0;
            _buttonBack.Click += (s, e) => ShowProfilesPage();
            ApplyRoundedCorners(_buttonBack, 6);
            _toolTip.SetToolTip(_buttonBack, "Back to profiles list.");

            // BIGGER logo in settings header
            var logoBox = new PictureBox
            {
                Size = new Size(32, 32),
                Location = new Point(_buttonBack.Right + 15, 14),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            if (_appLogoImage != null)
            {
                logoBox.Image = _appLogoImage;
            }
            else
            {
                logoBox.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(Color.FromArgb(55, 65, 81), 2))
                    {
                        e.Graphics.DrawRectangle(pen, 3, 3, logoBox.Width - 6, logoBox.Height - 6);
                    }
                };
            }

            _labelEditingProfile = new Label
            {
                AutoSize = true,
                Text = "Editing: (none)",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(logoBox.Right + 10, 19)
            };

            _buttonResetProfile = new Button
            {
                Text = "Reset profile",
                Width = 110,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentDanger,
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _buttonResetProfile.FlatAppearance.BorderSize = 0;
            ApplyRoundedCorners(_buttonResetProfile, 6);
            _buttonResetProfile.Click += ButtonResetProfile_Click;
            _toolTip.SetToolTip(_buttonResetProfile, "Reset speeds and keybinds only for this profile.");

            header.Controls.Add(_buttonBack);
            header.Controls.Add(logoBox);
            header.Controls.Add(_labelEditingProfile);
            header.Controls.Add(_buttonResetProfile);

            header.Resize += (s, e) =>
            {
                _buttonResetProfile.Location = new Point(
                    header.Width - _buttonResetProfile.Width - 20,
                    15);
            };

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgSettings,
                Padding = new Padding(0)
            };

            var settingsLayout = new Panel
            {
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill   // <-- important: let it fill the whole content area
            };

            // hero frame + big image
            var operatorFrame = new Panel
            {
                Size = new Size(280, 300),
                BackColor = CardNormalColor,
                Padding = new Padding(12)
            };
            ApplyRoundedCorners(operatorFrame, 12);
            operatorFrame.Location = new Point(0, 0);

            _pictureProfile = new PictureBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(15, 23, 42)
            };
            operatorFrame.Controls.Add(_pictureProfile);
            _toolTip.SetToolTip(_pictureProfile, "Preview of the selected operator.");

            var operatorCaption = new Label
            {
                AutoSize = true,
                Text = "Current operator",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular)
            };
            operatorCaption.Location = new Point(operatorFrame.Left + 4, operatorFrame.Bottom + 6);

            // ====== Weapons card (primary / secondary) – HORIZONTAL layout ======
            _weaponCard = new Panel
            {
                BackColor = CardNormalColor,
                Size = new Size(280, 240),
                Padding = new Padding(10)
            };
            ApplyRoundedCorners(_weaponCard, 8);
            _weaponCard.Location = new Point(operatorFrame.Left, operatorCaption.Bottom + 20);

            var weaponTitle = new Label
            {
                AutoSize = true,
                Text = "Weapons",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            var weaponUnderline = new Panel
            {
                Height = 2,
                BackColor = AccentPrimarySoft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // captions
            var primaryCaption = new Label
            {
                AutoSize = true,
                Text = "Primary weapon"
            };

            var secondaryCaption = new Label
            {
                AutoSize = true,
                Text = "Secondary weapon"
            };

            // primary controls
            _buttonPrimaryPrev = CreateFlatButton("<", 28, 24);
            _buttonPrimaryNext = CreateFlatButton(">", 28, 24);
            _labelPrimaryWeaponName = new Label
            {
                AutoSize = false,
                Text = "Primary weapon",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White
            };

            // secondary controls
            _buttonSecondaryPrev = CreateFlatButton("<", 28, 24);
            _buttonSecondaryNext = CreateFlatButton(">", 28, 24);
            _labelSecondaryWeaponName = new Label
            {
                AutoSize = false,
                Text = "Secondary weapon",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White
            };

            _picturePrimaryWeapon = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(15, 23, 42)
            };

            _pictureSecondaryWeapon = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(15, 23, 42)
            };

            _weaponCard.Controls.Add(weaponTitle);
            _weaponCard.Controls.Add(weaponUnderline);
            _weaponCard.Controls.Add(primaryCaption);
            _weaponCard.Controls.Add(secondaryCaption);
            _weaponCard.Controls.Add(_buttonPrimaryPrev);
            _weaponCard.Controls.Add(_buttonPrimaryNext);
            _weaponCard.Controls.Add(_labelPrimaryWeaponName);
            _weaponCard.Controls.Add(_buttonSecondaryPrev);
            _weaponCard.Controls.Add(_buttonSecondaryNext);
            _weaponCard.Controls.Add(_labelSecondaryWeaponName);
            _weaponCard.Controls.Add(_picturePrimaryWeapon);
            _weaponCard.Controls.Add(_pictureSecondaryWeapon);

            _buttonPrimaryPrev.Click += (s, e) => ChangeWeaponSelection(true, -1);
            _buttonPrimaryNext.Click += (s, e) => ChangeWeaponSelection(true, +1);
            _buttonSecondaryPrev.Click += (s, e) => ChangeWeaponSelection(false, -1);
            _buttonSecondaryNext.Click += (s, e) => ChangeWeaponSelection(false, +1);

            // local layout function for the weapons card
            void LayoutWeaponCard()
            {
                int padding = 10;
                int colGap = 16;

                int innerWidth = _weaponCard.ClientSize.Width - padding * 2;
                if (innerWidth < 100) innerWidth = 100;

                bool stacked =
                    innerWidth < 260; // fallback for super-narrow windows

                int colWidth;
                int xLeft = padding;
                int xRight;

                if (stacked)
                {
                    colWidth = innerWidth;
                    xRight = padding;
                }
                else
                {
                    colWidth = (innerWidth - colGap) / 2;
                    if (colWidth < 80) colWidth = 80;
                    xRight = padding + colWidth + colGap;
                }

                int top = 5;

                weaponTitle.Location = new Point(padding, top);
                weaponUnderline.Location = new Point(padding, weaponTitle.Bottom + 3);
                weaponUnderline.Width = innerWidth;

                int captionTop = weaponUnderline.Bottom + 7;
                primaryCaption.Location = new Point(xLeft, captionTop);
                secondaryCaption.Location = stacked
                    ? new Point(xLeft, primaryCaption.Bottom + 25)
                    : new Point(xRight, captionTop);

                int rowYPrimary = primaryCaption.Bottom + 5;
                _buttonPrimaryPrev.Location = new Point(xLeft, rowYPrimary);
                _buttonPrimaryNext.Location = new Point(xLeft + colWidth - _buttonPrimaryNext.Width, rowYPrimary);

                _labelPrimaryWeaponName.Location = new Point(_buttonPrimaryPrev.Right + 4, rowYPrimary + 3);
                _labelPrimaryWeaponName.Width =
                    Math.Max(40, _buttonPrimaryNext.Left - _buttonPrimaryPrev.Right - 8);
                _labelPrimaryWeaponName.Height = _buttonPrimaryNext.Height - 4;

                int rowYSecondary = secondaryCaption.Bottom + 5;
                _buttonSecondaryPrev.Location = new Point(stacked ? xLeft : xRight, rowYSecondary);
                _buttonSecondaryNext.Location = new Point(
                    (stacked ? xLeft : xRight) + colWidth - _buttonSecondaryNext.Width,
                    rowYSecondary);

                _labelSecondaryWeaponName.Location = new Point(_buttonSecondaryPrev.Right + 4, rowYSecondary + 3);
                _labelSecondaryWeaponName.Width =
                    Math.Max(40, _buttonSecondaryNext.Left - _buttonSecondaryPrev.Right - 8);
                _labelSecondaryWeaponName.Height = _buttonSecondaryNext.Height - 4;

                int imageTop =
                    Math.Max(_buttonPrimaryPrev.Bottom, _buttonSecondaryPrev.Bottom) + 8;
                int availableHeight = _weaponCard.ClientSize.Height - imageTop - padding;
                if (availableHeight < 40) availableHeight = 40;

                if (stacked)
                {
                    int half = (availableHeight - 6) / 2;
                    if (half < 40) half = 40;

                    _picturePrimaryWeapon.Bounds = new Rectangle(xLeft, imageTop, colWidth, half);
                    _pictureSecondaryWeapon.Bounds =
                        new Rectangle(xLeft, imageTop + half + 6, colWidth, half);
                }
                else
                {
                    _picturePrimaryWeapon.Bounds =
                        new Rectangle(xLeft, imageTop, colWidth, availableHeight);
                    _pictureSecondaryWeapon.Bounds =
                        new Rectangle(xRight, imageTop, colWidth, availableHeight);
                }
            }

            _weaponCard.Resize += (s, e) => LayoutWeaponCard();
            LayoutWeaponCard();
            // ===== end weapons card =====

            // movement card
            _movementCard = new Panel
            {
                BackColor = CardNormalColor,
                Size = new Size(460, 200),
                Padding = new Padding(10)
            };
            ApplyRoundedCorners(_movementCard, 8);
            _movementCard.Location = new Point(operatorFrame.Right + 40, operatorFrame.Top);

            var movementTitle = new Label
            {
                AutoSize = true,
                Text = "Movement",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(10, 5)
            };

            // thin accent line under movement title
            var movementUnderline = new Panel
            {
                Height = 2,
                Width = _movementCard.Width - 20,
                BackColor = AccentPrimarySoft,
                Location = new Point(10, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _labelHorizontal = new Label
            {
                AutoSize = true,
                Text = "Horizontal speed",
                Location = new Point(10, 35)
            };

            _trackBarHorizontal = new TrackBar
            {
                Minimum = HorizontalTrackBarMin,
                Maximum = HorizontalTrackBarMax,
                TickFrequency = 2000,
                Value = 0,
                Width = 310,
                Location = new Point(10, 55),
                BackColor = _movementCard.BackColor
            };
            _trackBarHorizontal.Scroll += TrackBarHorizontal_Scroll;
            _toolTip.SetToolTip(_trackBarHorizontal, "Adjust horizontal mouse speed.");

            _textHorizontal = new TextBox
            {
                Width = 80,
                Location = new Point(330, 55),
                Text = "0.000"
            };
            _textHorizontal.KeyDown += TextHorizontal_KeyDown;
            _textHorizontal.Leave += TextHorizontal_Leave;
            _toolTip.SetToolTip(_textHorizontal, "Precise horizontal speed value.");

            _labelVertical = new Label
            {
                AutoSize = true,
                Text = "Vertical speed",
                Location = new Point(10, 95)
            };

            _trackBarVertical = new TrackBar
            {
                Minimum = VerticalTrackBarMin,
                Maximum = VerticalTrackBarMax,
                TickFrequency = 2000,
                Value = 0,
                Width = 310,
                Location = new Point(10, 115),
                BackColor = _movementCard.BackColor
            };
            _trackBarVertical.Scroll += TrackBarVertical_Scroll;
            _toolTip.SetToolTip(_trackBarVertical, "Adjust vertical mouse speed.");

            _textVertical = new TextBox
            {
                Width = 80,
                Location = new Point(330, 115),
                Text = "0.000"
            };
            _textVertical.KeyDown += TextVertical_KeyDown;
            _textVertical.Leave += TextVertical_Leave;
            _toolTip.SetToolTip(_textVertical, "Precise vertical speed value.");

            _movementCard.Controls.Add(movementTitle);
            _movementCard.Controls.Add(movementUnderline);
            _movementCard.Controls.Add(_labelHorizontal);
            _movementCard.Controls.Add(_trackBarHorizontal);
            _movementCard.Controls.Add(_textHorizontal);
            _movementCard.Controls.Add(_labelVertical);
            _movementCard.Controls.Add(_trackBarVertical);
            _movementCard.Controls.Add(_textVertical);

            // setups card
            _setupCard = new Panel
            {
                BackColor = CardNormalColor,
                Size = new Size(460, 220),
                Padding = new Padding(10)
            };
            ApplyRoundedCorners(_setupCard, 8);
            _setupCard.Location = new Point(_movementCard.Left, _movementCard.Bottom + 20);

            var setupTitle = new Label
            {
                AutoSize = true,
                Text = "Setups & Keybinds",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(10, 5)
            };

            // thin accent line under setups title
            var setupUnderline = new Panel
            {
                Height = 2,
                Width = _setupCard.Width - 20,
                BackColor = AccentPrimarySoft,
                Location = new Point(10, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _labelActiveSetup = new Label
            {
                AutoSize = true,
                Text = "Active setup: 1 (Primary)",
                Location = new Point(10, 30),
                ForeColor = TextMuted
            };

            _labelSetup1 = new Label
            {
                AutoSize = true,
                Text = "PRIMARY – Setup 1 key:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(10, 60)
            };

            _textKey1 = new TextBox
            {
                ReadOnly = true,
                Width = 80,
                Location = new Point(210, 57),   // was 170
                Text = "None"
            };

            _buttonSetKey1 = CreateFlatButton("Set Key 1", 90, 28);
            _buttonSetKey1.Location = new Point(300, 55);  // was 260
            _buttonSetKey1.Click += (s, e) => StartCapturingKey(1);

            _buttonSaveSetup1 = CreateFlatButton("Save 1", 80, 28);
            _buttonSaveSetup1.Location = new Point(400, 55); // was 360

            _buttonSaveSetup1.Click += ButtonSaveSetup1_Click;
            _toolTip.SetToolTip(_buttonSaveSetup1, "Save current speeds as Setup 1.");


            _labelSetup2 = new Label
            {
                AutoSize = true,
                Text = "SECONDARY – Setup 2 key:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(10, 100)
            };

            _textKey2 = new TextBox
            {
                ReadOnly = true,
                Width = 80,
                Location = new Point(210, 97),   // was 170
                Text = "None"
            };

            _buttonSetKey2 = CreateFlatButton("Set Key 2", 90, 28);
            _buttonSetKey2.Location = new Point(300, 95);  // was 260
            _buttonSetKey2.Click += (s, e) => StartCapturingKey(2);

            _buttonSaveSetup2 = CreateFlatButton("Save 2", 80, 28);
            _buttonSaveSetup2.Location = new Point(400, 95); // was 360
            _buttonSaveSetup2.Click += ButtonSaveSetup2_Click;
            _toolTip.SetToolTip(_buttonSaveSetup2, "Save current speeds as Setup 2.");


            // NEW: Setup 3 (Maestro only)
            _labelSetup3 = new Label
            {
                AutoSize = true,
                Text = "Setup 3 (Maestro Camera):",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(10, 140)
            };

            _textKey3 = new TextBox
            {
                ReadOnly = true,
                Width = 80,
                Location = new Point(210, 137),
                Text = "None"
            };

            _buttonSetKey3 = CreateFlatButton("Set Key 3", 90, 28);
            _buttonSetKey3.Location = new Point(300, 135);
            _buttonSetKey3.Click += (s, e) => StartCapturingKey(3);

            _buttonSaveSetup3 = CreateFlatButton("Save 3", 80, 28);
            _buttonSaveSetup3.Location = new Point(400, 135);
            _buttonSaveSetup3.Click += ButtonSaveSetup3_Click;
            _toolTip.SetToolTip(_buttonSaveSetup3, "Save current speeds as Setup 3 (Maestro only).");


            _labelSetup1Summary = new Label
            {
                AutoSize = false,
                ForeColor = Color.Gold,
                BackColor = Color.FromArgb(31, 41, 55),           // darker strip
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Location = new Point(150, 200),
                Width = _setupCard.Width - 20,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 4, 8, 4),
                Text = "PRIMARY – H = 0.000, V = 0.000",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _labelSetup2Summary = new Label
            {
                AutoSize = false,
                ForeColor = Color.Cyan,
                BackColor = Color.FromArgb(31, 41, 55),           // slightly different shade
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Location = new Point(150, 232),
                Width = _setupCard.Width - 20,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 4, 8, 4),
                Text = "SECONDARY – H = 0.000, V = 0.000",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _labelSetup3Summary = new Label
            {
                AutoSize = false,
                ForeColor = Color.Cyan,
                BackColor = Color.FromArgb(31, 41, 55),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Location = new Point(150, 264),   // ⬅️ different Y than Setup 2
                Width = _setupCard.Width - 20,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 4, 8, 4),
                Text = "Setup 3 – H = 0.000, V = 0.000",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };




            ApplyRoundedCorners(_labelSetup1Summary, 6);
            ApplyRoundedCorners(_labelSetup2Summary, 6);
            ApplyRoundedCorners(_labelSetup3Summary, 6);


            _setupCard.Controls.Add(setupTitle);
            _setupCard.Controls.Add(setupUnderline);
            _setupCard.Controls.Add(_labelActiveSetup);

            // Setup 1
            _setupCard.Controls.Add(_labelSetup1);
            _setupCard.Controls.Add(_textKey1);
            _setupCard.Controls.Add(_buttonSetKey1);
            _setupCard.Controls.Add(_buttonSaveSetup1);

            // Setup 2
            _setupCard.Controls.Add(_labelSetup2);
            _setupCard.Controls.Add(_textKey2);
            _setupCard.Controls.Add(_buttonSetKey2);
            _setupCard.Controls.Add(_buttonSaveSetup2);

            // ✨ NEW: Setup 3 controls
            _setupCard.Controls.Add(_labelSetup3);
            _setupCard.Controls.Add(_textKey3);
            _setupCard.Controls.Add(_buttonSetKey3);
            _setupCard.Controls.Add(_buttonSaveSetup3);

            // Summary strips
            _setupCard.Controls.Add(_labelSetup1Summary);
            _setupCard.Controls.Add(_labelSetup2Summary);
            _setupCard.Controls.Add(_labelSetup3Summary);


            settingsLayout.Controls.Add(operatorFrame);
            settingsLayout.Controls.Add(operatorCaption);
            settingsLayout.Controls.Add(_weaponCard);      // Weapons card on the left
            settingsLayout.Controls.Add(_movementCard);    // Movement card on the right
            settingsLayout.Controls.Add(_setupCard);       // Setups card on the right

            content.Controls.Add(settingsLayout);

            // WATERMARK logo bottom-right on settings page (keep as before)
            _settingsLogoWatermark = new PictureBox
            {
                Size = new Size(80, 80),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            if (_appLogoImage != null)
            {
                _settingsLogoWatermark.Image = _appLogoImage;
            }
            else
            {
                _settingsLogoWatermark.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(Color.FromArgb(55, 65, 81), 2))
                    {
                        e.Graphics.DrawRectangle(pen, 4, 4,
                            _settingsLogoWatermark.Width - 8,
                            _settingsLogoWatermark.Height - 8);
                    }
                };
            }
            _toolTip.SetToolTip(_settingsLogoWatermark, "App logo watermark.");
            content.Controls.Add(_settingsLogoWatermark);

            // ---- NEW: responsive layout ----
            void Relayout()
            {
                int margin = 24;      // outer margin
                int colGap = 32;      // gap between left and right columns

                int cw = settingsLayout.ClientSize.Width;
                int ch = settingsLayout.ClientSize.Height;
                if (cw <= 0 || ch <= 0) return;

                // left column width ~ 30% of window, with minimum
                int leftWidth = Math.Max(260, (int)(cw * 0.32));
                // right column is the rest
                int rightWidth = cw - leftWidth - colGap - margin * 2;
                if (rightWidth < 320) rightWidth = 320;

                int topY = margin;

                // ---- Operator frame (top-left) ----
                int operatorHeight = Math.Max(260, (int)(ch * 0.45));
                operatorFrame.Bounds = new Rectangle(
                    margin,
                    topY,
                    leftWidth,
                    operatorHeight);

                // "Current operator" label directly under the image
                operatorCaption.Location = new Point(
                    operatorFrame.Left,
                    operatorFrame.Bottom + 6);

                // ---- Weapons card under the operator ----
                int weaponsTop = operatorCaption.Bottom + 8;
                int weaponsHeight = Math.Max(140, ch - weaponsTop - margin);
                _weaponCard.Bounds = new Rectangle(
                    margin,
                    weaponsTop,
                    leftWidth,
                    weaponsHeight);

                // ---- Right column: Movement + Setups ----
                int rightX = operatorFrame.Right + colGap;
                int movementHeight = Math.Max(160, (int)(ch * 0.30));

                _movementCard.Bounds = new Rectangle(
                    rightX,
                    topY,
                    rightWidth,
                    movementHeight);

                int setupsTop = _movementCard.Bottom + 16;
                int setupsHeight = ch - setupsTop - margin;

                _setupCard.Bounds = new Rectangle(
                    rightX,
                    setupsTop,
                    rightWidth,
                    Math.Max(280, setupsHeight));

            }

            // Layout once now and whenever the panel size changes
            settingsLayout.Resize += (s, e) => Relayout();
            Relayout();

            // Only move the watermark on resize now
            content.Resize += (s, e) => PositionSettingsLogoWatermark(content);
            PositionSettingsLogoWatermark(content);

            _pageSettings.Controls.Add(content);
            _pageSettings.Controls.Add(header);

            // keep your existing slider sync
            SyncHorizontalFromSlider();
            SyncVerticalFromSlider();
        }

        private Panel BuildSecondaryPageHeader(string title, EventHandler backButtonClick)
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = BgHeader
            };

            var backButton = CreateFlatButton("← Back", 80, 30);
            backButton.Location = new Point(15, 15);
            backButton.Click += backButtonClick;
            _toolTip.SetToolTip(backButton, "Back to profiles list.");

            var titleLabel = new Label
            {
                AutoSize = true,
                Text = title,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(backButton.Right + 10, 19)
            };

            header.Controls.Add(backButton);
            header.Controls.Add(titleLabel);

            return header;
        }

        // ===== About page =====
        private void BuildAboutPage()
        {
            var header = BuildSecondaryPageHeader("About", (s, e) => ShowProfilesPage());

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgSettings
            };

            var layout = new Panel
            {
                BackColor = Color.Transparent,
                Size = new Size(500, 320)
            };

            var logoBox = new PictureBox
            {
                Size = new Size(180, 180),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            // Make About logo circular
            MakePictureCircular(logoBox);

            // Use a different logo ONLY for the About page if available
            try
            {
                string aboutLogoPath = Path.Combine(_imagesFolder, "AboutLogo.png");
                if (File.Exists(aboutLogoPath))
                {
                    using (var img = Image.FromFile(aboutLogoPath))
                    {
                        logoBox.Image = new Bitmap(img);
                    }
                }
            }
            catch
            {
                // ignore, we'll just fall back to the drawn placeholder
            }

            // If no custom AboutLogo.png found, draw a simple placeholder
            if (logoBox.Image == null)
            {
                logoBox.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(AccentPrimarySoft, 3))
                    {
                        e.Graphics.DrawRectangle(pen, 8, 8, logoBox.Width - 16, logoBox.Height - 16);
                    }
                };
            }

            var madeByLabel = new Label
            {
                AutoSize = true,
                Text = "Made by GAMMO",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White
            };

            var githubLink = new LinkLabel
            {
                AutoSize = true,
                Text = "GitHub: JAD-CHADLI",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                LinkColor = AccentPrimary,
                ActiveLinkColor = Color.White,
                VisitedLinkColor = AccentPrimarySoft
            };
            githubLink.LinkBehavior = LinkBehavior.HoverUnderline;
            githubLink.LinkClicked += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/JAD-CHADLI",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    MessageBox.Show(
                        "Could not open the GitHub link.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            };

            layout.Controls.Add(logoBox);
            layout.Controls.Add(madeByLabel);
            layout.Controls.Add(githubLink);

            void Relayout()
            {
                logoBox.Location = new Point(
                    (layout.Width - logoBox.Width) / 2,
                    10);

                madeByLabel.Location = new Point(
                    (layout.Width - madeByLabel.Width) / 2,
                    logoBox.Bottom + 15);

                githubLink.Location = new Point(
                    (layout.Width - githubLink.Width) / 2,
                    madeByLabel.Bottom + 10);
            }

            layout.Resize += (s, e) => Relayout();
            Relayout();

            content.Controls.Add(layout);

            content.Resize += (s, e) => CenterInnerPanel(layout, content);
            CenterInnerPanel(layout, content);

            _pageAbout.Controls.Add(content);
            _pageAbout.Controls.Add(header);
        }

        // ===== Tutorial page =====
        private void BuildTutorialPage()
        {
            var header = BuildSecondaryPageHeader("Tutorial", (s, e) => ShowProfilesPage());

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgSettings
            };

            var layout = new Panel
            {
                BackColor = Color.Transparent,
                Size = new Size(700, 400)
            };

            var tutorialTitle = new Label
            {
                AutoSize = true,
                Text = "How to use GM R6 Recoil Control",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White
            };

            var tutorialBody = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(650, 0),
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                Text =
        "1. Choose a side (Attackers / Defenders) at the top.\n" +
        "2. Use the search box or scroll to find the operator you want, then click its card to select it.\n" +
        "3. Press \"Modify\" on a profile to open the settings page.\n" +
        "4. In the Weapons card, use the arrows to choose your primary and secondary weapons.\n" +
        "5. In the Movement card, set Horizontal and Vertical speed. These values are saved per weapon.\n" +
        "6. In Setups & Keybinds:\n" +
        "   • Choose a key for Setup 1 (Primary) and click \"Save 1\" to store recoil for the selected primary weapon.\n" +
        "   • Choose a key for Setup 2 (Secondary) and click \"Save 2\" to store recoil for the selected secondary weapon.\n" +
        "   • Maestro also has an extra Setup 3 (camera) with its own key and speeds.\n" +
        "7. On this Tutorial page, set a global Start / Stop key if you want, or use the \"Start\" button on the main page.\n" +
        "8. When the tool is active: hold RIGHT mouse button, then press and hold LEFT mouse button to start the recoil movement.\n" +
        "9. In-game, press your setup keys to switch between Setup 1 / Setup 2 (and Setup 3 on Maestro) for the selected operator.\n" +
        "10. Use \"Reset profile\" to clear settings for the current operator, or \"RESET ALL\" on this page to clear all profiles.\n" +
        "11. Use \"Export settings\" to save your config to a .json file, and \"Import settings\" to load one (for example from a friend)."
            };

            var toggleLabel = new Label
            {
                AutoSize = true,
                Text = "Start / Stop key:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White
            };

            _textToggleKey = new TextBox
            {
                ReadOnly = true,
                Width = 80,
                Text = "None"
            };

            _buttonSetToggleKey = CreateFlatButton("Set key", 80, 30);
            _buttonSetToggleKey.Click += (s, e) => StartCapturingToggleKey();
            _toolTip.SetToolTip(_buttonSetToggleKey, "Choose a hotkey that toggles Start / Stop.");




            layout.Controls.Add(tutorialTitle);
            layout.Controls.Add(tutorialBody);

            void Relayout()
            {
                tutorialTitle.Location = new Point(
                    (layout.Width - tutorialTitle.Width) / 2,
                    10);

                tutorialBody.Location = new Point(
                    (layout.Width - tutorialBody.Width) / 2,
                    tutorialTitle.Bottom + 20);
            }

            layout.Resize += (s, e) => Relayout();
            Relayout();

            content.Controls.Add(layout);

            // RESET ALL button bottom-right INSIDE the tutorial page
            _buttonResetAll = CreateFlatButton("RESET ALL", 120, 30);
            _buttonResetAll.BackColor = AccentDanger;
            _buttonResetAll.ForeColor = Color.White;
            _buttonResetAll.FlatAppearance.BorderSize = 0;
            _buttonResetAll.Click += ButtonResetAll_Click;
            _toolTip.SetToolTip(_buttonResetAll, "Reset speeds and keybinds for all profiles.");
            content.Controls.Add(_buttonResetAll);

            content.Controls.Add(toggleLabel);
            content.Controls.Add(_textToggleKey);
            content.Controls.Add(_buttonSetToggleKey);


            // NEW: Export / Import buttons (bottom-left)
            _buttonExportSettings = CreateFlatButton("Export settings", 140, 30);
            _buttonImportSettings = CreateFlatButton("Import settings", 140, 30);

            _buttonExportSettings.Click += ButtonExportSettings_Click;
            _buttonImportSettings.Click += ButtonImportSettings_Click;

            _toolTip.SetToolTip(_buttonExportSettings, "Save your current settings to a .json file you can share.");
            _toolTip.SetToolTip(_buttonImportSettings, "Load settings from a .json file (for example from a friend).");

            content.Controls.Add(_buttonExportSettings);
            content.Controls.Add(_buttonImportSettings);

            // Center tutorial text and keep buttons at bottom
            content.Resize += (s, e) =>
            {
                CenterInnerPanel(layout, content);

                int margin = 20;

                // bottom-right: RESET ALL
                _buttonResetAll.Location = new Point(
                    content.ClientSize.Width - _buttonResetAll.Width - margin,
                    content.ClientSize.Height - _buttonResetAll.Height - margin);

                // bottom-left: Export / Import
                _buttonExportSettings.Location = new Point(
                    margin,
                    content.ClientSize.Height - _buttonExportSettings.Height - margin);

                _buttonImportSettings.Location = new Point(
                    _buttonExportSettings.Right + 10,
                    _buttonExportSettings.Top);

                // 🔽 bottom-center: Start / Stop key controls
                int toggleY = content.ClientSize.Height - _buttonResetAll.Height - margin;
                int totalToggleWidth = toggleLabel.Width + 8 + _textToggleKey.Width + 8 + _buttonSetToggleKey.Width;
                int toggleX = (content.ClientSize.Width - totalToggleWidth) / 2;

                if (toggleX < margin) toggleX = margin;

                toggleLabel.Location = new Point(toggleX, toggleY + 5);
                _textToggleKey.Location = new Point(toggleLabel.Right + 8, toggleY + 2);
                _buttonSetToggleKey.Location = new Point(_textToggleKey.Right + 8, toggleY);
            };




            // Initial positioning
            CenterInnerPanel(layout, content);
            int initMargin = 20;

            _buttonResetAll.Location = new Point(
                content.ClientSize.Width - _buttonResetAll.Width - initMargin,
                content.ClientSize.Height - _buttonResetAll.Height - initMargin);

            _buttonExportSettings.Location = new Point(
                initMargin,
                content.ClientSize.Height - _buttonExportSettings.Height - initMargin);

            _buttonImportSettings.Location = new Point(
                _buttonExportSettings.Right + 10,
                _buttonExportSettings.Top);

            _pageTutorial.Controls.Add(content);
            _pageTutorial.Controls.Add(header);
        }


        private Button CreateFlatButton(string text, int width, int height)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 85, 99);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(31, 41, 55);
            ApplyRoundedCorners(btn, 6);
            return btn;
        }

        private void StyleSegmentButton(Button button, bool isActive)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Width = 110;
            button.Height = 30;
            button.Font = new Font("Segoe UI", 9F, isActive ? FontStyle.Bold : FontStyle.Regular);

            if (isActive)
            {
                button.BackColor = AccentPrimary;
                button.ForeColor = Color.White;
            }
            else
            {
                button.BackColor = CardNormalColor;
                button.ForeColor = TextMuted;
            }
        }

        private void ApplyRoundedCorners(Control control, int radius)
        {
            void UpdateRegion(object? sender, EventArgs e)
            {
                if (control.Width <= 0 || control.Height <= 0)
                    return;

                int d = radius * 2;
                var rect = new Rectangle(0, 0, control.Width, control.Height);

                using (var path = new GraphicsPath())
                {
                    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    control.Region = new Region(path);
                }
            }

            control.HandleCreated += UpdateRegion;
            control.Resize += UpdateRegion;
        }

        // Helper to make a PictureBox circular (for About logo)
        private void MakePictureCircular(PictureBox pb)
        {
            void UpdateRegion(object? sender, EventArgs e)
            {
                if (pb.Width <= 0 || pb.Height <= 0)
                    return;

                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(0, 0, pb.Width - 1, pb.Height - 1);
                    pb.Region = new Region(path);
                }
            }

            pb.HandleCreated += UpdateRegion;
            pb.Resize += UpdateRegion;
        }

        private void CenterInnerPanel(Control inner, Control outer)
        {
            int x = (outer.ClientSize.Width - inner.Width) / 2;
            int y = (outer.ClientSize.Height - inner.Height) / 2;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            inner.Location = new Point(x, y);
        }

        private void PositionSettingsLogoWatermark(Control content)
        {
            if (_settingsLogoWatermark == null) return;
            int margin = 18;
            int x = content.ClientSize.Width - _settingsLogoWatermark.Width - margin;
            int y = content.ClientSize.Height - _settingsLogoWatermark.Height - margin;
            if (x < margin) x = margin;
            if (y < margin) y = margin;
            _settingsLogoWatermark.Location = new Point(x, y);
        }

        // smooth drawing (reduce flicker)
        private void SetDoubleBuffered(Control c)
        {
            if (SystemInformation.TerminalServerSession)
                return;

            var property = typeof(Control).GetProperty(
                "DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);

            property?.SetValue(c, true, null);
        }

        // ==========================================================
        // Page switching
        // ==========================================================
        private void ShowPage(Panel page)
        {
            _pageProfiles.Visible = page == _pageProfiles;
            _pageSettings.Visible = page == _pageSettings;
            _pageAbout.Visible = page == _pageAbout;
            _pageTutorial.Visible = page == _pageTutorial;

            page.BringToFront();
        }

        private void ShowProfilesPage()
        {
            ShowPage(_pageProfiles);
        }

        private void ShowSettingsPage()
        {
            if (_currentProfile == null)
                return;

            _labelEditingProfile.Text = $"Editing: {_currentProfile.Name}";
            LoadProfileImage(_currentProfile.ImageFileName);
            ShowPage(_pageSettings);
        }

        private void ShowAboutPage()
        {
            ShowPage(_pageAbout);
        }

        private void StartCapturingToggleKey()
        {
            _capturingToggleKey = true;
            _capturingKeyForSetup = 0;
            if (_textToggleKey != null)
                _textToggleKey.Text = "Press key...";

            // make sure the form receives the KeyDown event
            ActiveControl = this;
        }


        private void ShowTutorialPage()
        {
            ShowPage(_pageTutorial);
        }

        // ==========================================================
        // Create profiles (static names & images)
        // ==========================================================
        private void CreateProfiles()
        {
            // Category A – Attackers
            _profiles.Add(new Profile("A", 37, "Striker", "A_Striker.png"));   // first

            _profiles.Add(new Profile("A", 1, "Sledge", "A_Sledge.png"));
            _profiles.Add(new Profile("A", 2, "Thatcher", "A_Thatcher.png"));
            _profiles.Add(new Profile("A", 3, "Ash", "A_Ash.png"));
            _profiles.Add(new Profile("A", 4, "Thermite", "A_Thermite.png"));
            _profiles.Add(new Profile("A", 5, "Twitch", "A_Twitch.png"));
            _profiles.Add(new Profile("A", 6, "Montagne", "A_Montagne.png"));
            _profiles.Add(new Profile("A", 7, "Glaz", "A_Glaz.png"));
            _profiles.Add(new Profile("A", 8, "Fuze", "A_Fuze.png"));
            _profiles.Add(new Profile("A", 9, "Blitz", "A_Blitz.png"));
            _profiles.Add(new Profile("A", 10, "IQ", "A_IQ.png"));
            _profiles.Add(new Profile("A", 11, "Buck", "A_Buck.png"));
            _profiles.Add(new Profile("A", 12, "Blackbeard", "A_Blackbeard.png"));
            _profiles.Add(new Profile("A", 13, "Capitão", "A_Capitao.png"));
            _profiles.Add(new Profile("A", 14, "Hibana", "A_Hibana.png"));
            _profiles.Add(new Profile("A", 15, "Jackal", "A_Jackal.png"));
            _profiles.Add(new Profile("A", 16, "Ying", "A_Ying.png"));
            _profiles.Add(new Profile("A", 17, "Zofia", "A_Zofia.png"));
            _profiles.Add(new Profile("A", 18, "Dokkaebi", "A_Dokkaebi.png"));
            _profiles.Add(new Profile("A", 19, "Lion", "A_Lion.png"));
            _profiles.Add(new Profile("A", 20, "Finka", "A_Finka.png"));
            _profiles.Add(new Profile("A", 21, "Maverick", "A_Maverick.png"));
            _profiles.Add(new Profile("A", 22, "Nomad", "A_Nomad.png"));
            _profiles.Add(new Profile("A", 23, "Gridlock", "A_Gridlock.png"));
            _profiles.Add(new Profile("A", 24, "Nøkk", "A_Nokk.png"));
            _profiles.Add(new Profile("A", 25, "Amaru", "A_Amaru.png"));
            _profiles.Add(new Profile("A", 26, "Kali", "A_Kali.png"));
            _profiles.Add(new Profile("A", 27, "Iana", "A_Iana.png"));
            _profiles.Add(new Profile("A", 28, "Ace", "A_Ace.png"));
            _profiles.Add(new Profile("A", 29, "Zero", "A_Zero.png"));
            _profiles.Add(new Profile("A", 30, "Flores", "A_Flores.png"));
            _profiles.Add(new Profile("A", 31, "Osa", "A_Osa.png"));
            _profiles.Add(new Profile("A", 32, "Sens", "A_Sens.png"));
            _profiles.Add(new Profile("A", 33, "Grim", "A_Grim.png"));
            _profiles.Add(new Profile("A", 34, "Brava", "A_Brava.png"));
            _profiles.Add(new Profile("A", 35, "Ram", "A_Ram.png"));
            _profiles.Add(new Profile("A", 39, "Deimos", "A_Deimos.png"));
            _profiles.Add(new Profile("A", 38, "Rauora", "A_Rauora.png"));

            // Category B – Defenders
            _profiles.Add(new Profile("B", 36, "Sentry", "B_Sentry.png"));     // first

            _profiles.Add(new Profile("B", 1, "Smoke", "B_Smoke.png"));
            _profiles.Add(new Profile("B", 2, "Mute", "B_Mute.png"));
            _profiles.Add(new Profile("B", 3, "Castle", "B_Castle.png"));
            _profiles.Add(new Profile("B", 4, "Pulse", "B_Pulse.png"));
            _profiles.Add(new Profile("B", 5, "Doc", "B_Doc.png"));
            _profiles.Add(new Profile("B", 6, "Rook", "B_Rook.png"));
            _profiles.Add(new Profile("B", 7, "Kapkan", "B_Kapkan.png"));
            _profiles.Add(new Profile("B", 8, "Tachanka", "B_Tachanka.png"));
            _profiles.Add(new Profile("B", 9, "Jäger", "B_Jager.png"));
            _profiles.Add(new Profile("B", 10, "Bandit", "B_Bandit.png"));
            _profiles.Add(new Profile("B", 11, "Frost", "B_Frost.png"));
            _profiles.Add(new Profile("B", 12, "Valkyrie", "B_Valkyrie.png"));
            _profiles.Add(new Profile("B", 13, "Caveira", "B_Caveira.png"));
            _profiles.Add(new Profile("B", 14, "Echo", "B_Echo.png"));
            _profiles.Add(new Profile("B", 15, "Mira", "B_Mira.png"));
            _profiles.Add(new Profile("B", 16, "Lesion", "B_Lesion.png"));
            _profiles.Add(new Profile("B", 17, "Ela", "B_Ela.png"));
            _profiles.Add(new Profile("B", 18, "Vigil", "B_Vigil.png"));
            _profiles.Add(new Profile("B", 19, "Maestro", "B_Maestro.png"));
            _profiles.Add(new Profile("B", 20, "Alibi", "B_Alibi.png"));
            _profiles.Add(new Profile("B", 21, "Clash", "B_Clash.png"));
            _profiles.Add(new Profile("B", 22, "Kaid", "B_Kaid.png"));
            _profiles.Add(new Profile("B", 23, "Mozzie", "B_Mozzie.png"));
            _profiles.Add(new Profile("B", 24, "Warden", "B_Warden.png"));
            _profiles.Add(new Profile("B", 25, "Goyo", "B_Goyo.png"));
            _profiles.Add(new Profile("B", 26, "Wamai", "B_Wamai.png"));
            _profiles.Add(new Profile("B", 27, "Oryx", "B_Oryx.png"));
            _profiles.Add(new Profile("B", 28, "Melusi", "B_Melusi.png"));
            _profiles.Add(new Profile("B", 29, "Thunderbird", "B_Thunderbird.png"));
            _profiles.Add(new Profile("B", 30, "Aruni", "B_Aruni.png"));

            // Thorn before Azami
            _profiles.Add(new Profile("B", 38, "Thorn", "B_Thorn.png"));

            _profiles.Add(new Profile("B", 31, "Azami", "B_Azami.png"));
            _profiles.Add(new Profile("B", 32, "Solis", "B_Solis.png"));
            _profiles.Add(new Profile("B", 33, "Fenrir", "B_Fenrir.png"));
            _profiles.Add(new Profile("B", 34, "Tubarão", "B_Tubarao.png"));
            _profiles.Add(new Profile("B", 35, "Skopós", "B_Skopos.png"));
            _profiles.Add(new Profile("B", 37, "Denari", "B_Denari.png"));

            // Hidden global profile used only to store global hotkey etc.
            _globalProfile = new Profile("G", 0, "GlobalSettings", null);
            _profiles.Add(_globalProfile);

        }

        // ==========================================================
        // Weapons per operator
        // ==========================================================
        private void InitializeWeapons()
        {
            // ATTACKERS
            var striker = _profiles.Find(p => p.Name == "Striker");
            if (striker != null)
            {
                striker.PrimaryWeapons.Clear();
                striker.SecondaryWeapons.Clear();
                striker.PrimaryWeapons.Add(new WeaponInfo("M4", "W_M4.png"));
                striker.PrimaryWeapons.Add(new WeaponInfo("M249", "W_M249.png"));
                striker.PrimaryWeapons.Add(new WeaponInfo("SR-25", "W_SR25.png"));
                striker.SecondaryWeapons.Add(new WeaponInfo("5.7 USG", "W_57USG.png"));
                striker.SecondaryWeapons.Add(new WeaponInfo("ITA12S", "W_ITA12S.png"));
                striker.SelectedPrimaryIndex = 0;
                striker.SelectedSecondaryIndex = 0;
            }

            var sledge = _profiles.Find(p => p.Name == "Sledge");
            if (sledge != null)
            {
                sledge.PrimaryWeapons.Clear();
                sledge.SecondaryWeapons.Clear();
                sledge.PrimaryWeapons.Add(new WeaponInfo("L85A2", "W_L85A2.png"));
                sledge.PrimaryWeapons.Add(new WeaponInfo("M590A1", "W_M590A1.png"));
                sledge.SecondaryWeapons.Add(new WeaponInfo("P226 Mk 25", "W_P226MK25.png"));
                sledge.SecondaryWeapons.Add(new WeaponInfo("Reaper MK2", "W_REAPERMK2.png"));
                sledge.SelectedPrimaryIndex = 0;
                sledge.SelectedSecondaryIndex = 0;
            }

            var thatcher = _profiles.Find(p => p.Name == "Thatcher");
            if (thatcher != null)
            {
                thatcher.PrimaryWeapons.Clear();
                thatcher.SecondaryWeapons.Clear();
                thatcher.PrimaryWeapons.Add(new WeaponInfo("AR33", "W_AR33.png"));
                thatcher.PrimaryWeapons.Add(new WeaponInfo("L85A2", "W_L85A2.png"));
                thatcher.PrimaryWeapons.Add(new WeaponInfo("M590A1", "W_M590A1.png"));
                thatcher.PrimaryWeapons.Add(new WeaponInfo("PMR90A2", "W_PMR90A2.png"));
                thatcher.SecondaryWeapons.Add(new WeaponInfo("P226 Mk 25", "W_P226MK25.png"));
                thatcher.SecondaryWeapons.Add(new WeaponInfo("SMG-11", "W_SMG11.png"));
                thatcher.SelectedPrimaryIndex = 0;
                thatcher.SelectedSecondaryIndex = 0;
            }

            var ash = _profiles.Find(p => p.Name == "Ash");
            if (ash != null)
            {
                ash.PrimaryWeapons.Clear();
                ash.SecondaryWeapons.Clear();
                ash.PrimaryWeapons.Add(new WeaponInfo("R4-C", "W_R4C.png"));
                ash.PrimaryWeapons.Add(new WeaponInfo("G36C", "W_G36C.png"));
                ash.SecondaryWeapons.Add(new WeaponInfo("5.7 USG", "W_57USG.png"));
                ash.SecondaryWeapons.Add(new WeaponInfo("M45 MEUSOC", "W_M45MEUSOC.png"));
                ash.SelectedPrimaryIndex = 0;
                ash.SelectedSecondaryIndex = 0;
            }

            var thermite = _profiles.Find(p => p.Name == "Thermite");
            if (thermite != null)
            {
                thermite.PrimaryWeapons.Clear();
                thermite.SecondaryWeapons.Clear();
                thermite.PrimaryWeapons.Add(new WeaponInfo("556xi", "W_556XI.png"));
                thermite.PrimaryWeapons.Add(new WeaponInfo("M1014", "W_M1014.png"));
                thermite.SecondaryWeapons.Add(new WeaponInfo("5.7 USG", "W_57USG.png"));
                thermite.SecondaryWeapons.Add(new WeaponInfo("M45 MEUSOC", "W_M45MEUSOC.png"));
                thermite.SelectedPrimaryIndex = 0;
                thermite.SelectedSecondaryIndex = 0;
            }

            var twitch = _profiles.Find(p => p.Name == "Twitch");
            if (twitch != null)
            {
                twitch.PrimaryWeapons.Clear();
                twitch.SecondaryWeapons.Clear();
                twitch.PrimaryWeapons.Add(new WeaponInfo("F2", "W_F2.png"));
                twitch.PrimaryWeapons.Add(new WeaponInfo("417", "W_417.png"));
                twitch.PrimaryWeapons.Add(new WeaponInfo("SG-CQB", "W_SGCQB.png"));
                twitch.SecondaryWeapons.Add(new WeaponInfo("P9", "W_P9.png"));
                twitch.SecondaryWeapons.Add(new WeaponInfo("LFP586", "W_LFP586.png"));
                twitch.SelectedPrimaryIndex = 0;
                twitch.SelectedSecondaryIndex = 0;
            }

            var montagne = _profiles.Find(p => p.Name == "Montagne");
            if (montagne != null)
            {
                montagne.PrimaryWeapons.Clear();
                montagne.SecondaryWeapons.Clear();
                montagne.PrimaryWeapons.Add(new WeaponInfo("Le Roc Extendable Shield", "W_LEROCEXTENDABLESHIELD.png"));
                montagne.SecondaryWeapons.Add(new WeaponInfo("P9", "W_P9.png"));
                montagne.SecondaryWeapons.Add(new WeaponInfo("LFP586", "W_LFP586.png"));
                montagne.SelectedPrimaryIndex = 0;
                montagne.SelectedSecondaryIndex = 0;
            }

            var glaz = _profiles.Find(p => p.Name == "Glaz");
            if (glaz != null)
            {
                glaz.PrimaryWeapons.Clear();
                glaz.SecondaryWeapons.Clear();
                glaz.PrimaryWeapons.Add(new WeaponInfo("OTs-03", "W_OTS03.png"));
                glaz.SecondaryWeapons.Add(new WeaponInfo("PMM", "W_PMM.png"));
                glaz.SecondaryWeapons.Add(new WeaponInfo("GONNE-6", "W_GONNE6.png"));
                glaz.SecondaryWeapons.Add(new WeaponInfo("Bearing 9", "W_BEARING9.png"));
                glaz.SelectedPrimaryIndex = 0;
                glaz.SelectedSecondaryIndex = 0;
            }

            var fuze = _profiles.Find(p => p.Name == "Fuze");
            if (fuze != null)
            {
                fuze.PrimaryWeapons.Clear();
                fuze.SecondaryWeapons.Clear();
                fuze.PrimaryWeapons.Add(new WeaponInfo("AK-12", "W_AK12.png"));
                fuze.PrimaryWeapons.Add(new WeaponInfo("6P41", "W_6P41.png"));
                fuze.PrimaryWeapons.Add(new WeaponInfo("Ballistic Shield", "W_BALLISTICSHIELD.png"));
                fuze.SecondaryWeapons.Add(new WeaponInfo("PMM", "W_PMM.png"));
                fuze.SecondaryWeapons.Add(new WeaponInfo("GSh-18", "W_GSH18.png"));
                fuze.SelectedPrimaryIndex = 0;
                fuze.SelectedSecondaryIndex = 0;
            }

            var blitz = _profiles.Find(p => p.Name == "Blitz");
            if (blitz != null)
            {
                blitz.PrimaryWeapons.Clear();
                blitz.SecondaryWeapons.Clear();
                blitz.PrimaryWeapons.Add(new WeaponInfo("G52 Tactical Shield", "W_G52TACTICALSHIELD.png"));
                blitz.SecondaryWeapons.Add(new WeaponInfo("P12", "W_P12.png"));
                blitz.SelectedPrimaryIndex = 0;
                blitz.SelectedSecondaryIndex = 0;
            }

            var iq = _profiles.Find(p => p.Name == "IQ");
            if (iq != null)
            {
                iq.PrimaryWeapons.Clear();
                iq.SecondaryWeapons.Clear();
                iq.PrimaryWeapons.Add(new WeaponInfo("AUG A2", "W_AUGA2.png"));
                iq.PrimaryWeapons.Add(new WeaponInfo("552 Commando", "W_552COMMANDO.png"));
                iq.PrimaryWeapons.Add(new WeaponInfo("G8A1", "W_G8A1.png"));
                iq.SecondaryWeapons.Add(new WeaponInfo("P12", "W_P12.png"));
                iq.SelectedPrimaryIndex = 0;
                iq.SelectedSecondaryIndex = 0;
            }

            var buck = _profiles.Find(p => p.Name == "Buck");
            if (buck != null)
            {
                buck.PrimaryWeapons.Clear();
                buck.SecondaryWeapons.Clear();
                buck.PrimaryWeapons.Add(new WeaponInfo("C8-SFW", "W_C8SFW.png"));
                buck.PrimaryWeapons.Add(new WeaponInfo("CAMRS", "W_CAMRS.png"));
                buck.SecondaryWeapons.Add(new WeaponInfo("Mk1 9mm", "W_MK19MM.png"));
                buck.SelectedPrimaryIndex = 0;
                buck.SelectedSecondaryIndex = 0;
            }

            var blackbeard = _profiles.Find(p => p.Name == "Blackbeard");
            if (blackbeard != null)
            {
                blackbeard.PrimaryWeapons.Clear();
                blackbeard.SecondaryWeapons.Clear();
                blackbeard.PrimaryWeapons.Add(new WeaponInfo("MK17 CQB", "W_MK17CQB.png"));
                blackbeard.PrimaryWeapons.Add(new WeaponInfo("SR-25", "W_SR25.png"));
                blackbeard.SelectedPrimaryIndex = 0;
                blackbeard.SelectedSecondaryIndex = 0;
            }

            var capitao = _profiles.Find(p => p.Name == "Capitão");
            if (capitao != null)
            {
                capitao.PrimaryWeapons.Clear();
                capitao.SecondaryWeapons.Clear();
                capitao.PrimaryWeapons.Add(new WeaponInfo("PARA-308", "W_PARA308.png"));
                capitao.PrimaryWeapons.Add(new WeaponInfo("M249", "W_M249.png"));
                capitao.SecondaryWeapons.Add(new WeaponInfo("PRB92", "W_PRB92.png"));
                capitao.SecondaryWeapons.Add(new WeaponInfo("GONNE-6", "W_GONNE6.png"));
                capitao.SelectedPrimaryIndex = 0;
                capitao.SelectedSecondaryIndex = 0;
            }

            var hibana = _profiles.Find(p => p.Name == "Hibana");
            if (hibana != null)
            {
                hibana.PrimaryWeapons.Clear();
                hibana.SecondaryWeapons.Clear();
                hibana.PrimaryWeapons.Add(new WeaponInfo("Type-89", "W_TYPE89.png"));
                hibana.PrimaryWeapons.Add(new WeaponInfo("SuperNova", "W_SUPERNOVA.png"));
                hibana.SecondaryWeapons.Add(new WeaponInfo("P229", "W_P229.png"));
                hibana.SecondaryWeapons.Add(new WeaponInfo("Bearing 9", "W_BEARING9.png"));
                hibana.SelectedPrimaryIndex = 0;
                hibana.SelectedSecondaryIndex = 0;
            }

            var jackal = _profiles.Find(p => p.Name == "Jackal");
            if (jackal != null)
            {
                jackal.PrimaryWeapons.Clear();
                jackal.SecondaryWeapons.Clear();
                jackal.PrimaryWeapons.Add(new WeaponInfo("C7E", "W_C7E.png"));
                jackal.PrimaryWeapons.Add(new WeaponInfo("PDW9", "W_PDW9.png"));
                jackal.PrimaryWeapons.Add(new WeaponInfo("ITA12L", "W_ITA12L.png"));
                jackal.SecondaryWeapons.Add(new WeaponInfo("USP40", "W_USP40.png"));
                jackal.SecondaryWeapons.Add(new WeaponInfo("ITA12S", "W_ITA12S.png"));
                jackal.SelectedPrimaryIndex = 0;
                jackal.SelectedSecondaryIndex = 0;
            }

            var ying = _profiles.Find(p => p.Name == "Ying");
            if (ying != null)
            {
                ying.PrimaryWeapons.Clear();
                ying.SecondaryWeapons.Clear();
                ying.PrimaryWeapons.Add(new WeaponInfo("T-95 LSW", "W_T95LSW.png"));
                ying.PrimaryWeapons.Add(new WeaponInfo("SIX12", "W_SIX12.png"));
                ying.SecondaryWeapons.Add(new WeaponInfo("Q-929", "W_Q929.png"));
                ying.SecondaryWeapons.Add(new WeaponInfo("Reaper MK2", "W_REAPERMK2.png"));
                ying.SelectedPrimaryIndex = 0;
                ying.SelectedSecondaryIndex = 0;
            }

            var zofia = _profiles.Find(p => p.Name == "Zofia");
            if (zofia != null)
            {
                zofia.PrimaryWeapons.Clear();
                zofia.SecondaryWeapons.Clear();
                zofia.PrimaryWeapons.Add(new WeaponInfo("M762", "W_M762.png"));
                zofia.PrimaryWeapons.Add(new WeaponInfo("LMG-E", "W_LMGE.png"));
                zofia.SecondaryWeapons.Add(new WeaponInfo("RG15", "W_RG15.png"));
                zofia.SelectedPrimaryIndex = 0;
                zofia.SelectedSecondaryIndex = 0;
            }

            var dokkaebi = _profiles.Find(p => p.Name == "Dokkaebi");
            if (dokkaebi != null)
            {
                dokkaebi.PrimaryWeapons.Clear();
                dokkaebi.SecondaryWeapons.Clear();
                dokkaebi.PrimaryWeapons.Add(new WeaponInfo("Mk 14 EBR", "W_MK14EBR.png"));
                dokkaebi.PrimaryWeapons.Add(new WeaponInfo("BOSG.12.2", "W_BOSG122.png"));
                dokkaebi.SecondaryWeapons.Add(new WeaponInfo("SMG-12", "W_SMG12.png"));
                dokkaebi.SecondaryWeapons.Add(new WeaponInfo("GONNE-6", "W_GONNE6.png"));
                dokkaebi.SecondaryWeapons.Add(new WeaponInfo("C75 Auto", "W_C75AUTO.png"));
                dokkaebi.SelectedPrimaryIndex = 0;
                dokkaebi.SelectedSecondaryIndex = 0;
            }

            var lion = _profiles.Find(p => p.Name == "Lion");
            if (lion != null)
            {
                lion.PrimaryWeapons.Clear();
                lion.SecondaryWeapons.Clear();
                lion.PrimaryWeapons.Add(new WeaponInfo("V308", "W_V308.png"));
                lion.PrimaryWeapons.Add(new WeaponInfo("417", "W_417.png"));
                lion.PrimaryWeapons.Add(new WeaponInfo("SG-CQB", "W_SGCQB.png"));
                lion.SecondaryWeapons.Add(new WeaponInfo("P9", "W_P9.png"));
                lion.SecondaryWeapons.Add(new WeaponInfo("LFP586", "W_LFP586.png"));
                lion.SelectedPrimaryIndex = 0;
                lion.SelectedSecondaryIndex = 0;
            }

            var finka = _profiles.Find(p => p.Name == "Finka");
            if (finka != null)
            {
                finka.PrimaryWeapons.Clear();
                finka.SecondaryWeapons.Clear();
                finka.PrimaryWeapons.Add(new WeaponInfo("Spear .308", "W_SPEAR308.png"));
                finka.PrimaryWeapons.Add(new WeaponInfo("6P41", "W_6P41.png"));
                finka.PrimaryWeapons.Add(new WeaponInfo("SASG-12", "W_SASG12.png"));
                finka.SecondaryWeapons.Add(new WeaponInfo("PMM", "W_PMM.png"));
                finka.SecondaryWeapons.Add(new WeaponInfo("GSh-18", "W_GSH18.png"));
                finka.SelectedPrimaryIndex = 0;
                finka.SelectedSecondaryIndex = 0;
            }

            var maverick = _profiles.Find(p => p.Name == "Maverick");
            if (maverick != null)
            {
                maverick.PrimaryWeapons.Clear();
                maverick.SecondaryWeapons.Clear();
                maverick.PrimaryWeapons.Add(new WeaponInfo("AR-15.50", "W_AR1550.png"));
                maverick.PrimaryWeapons.Add(new WeaponInfo("M4", "W_M4.png"));
                maverick.SecondaryWeapons.Add(new WeaponInfo("1911 TACOPS", "W_1911TACOPS.png"));
                maverick.SelectedPrimaryIndex = 0;
                maverick.SelectedSecondaryIndex = 0;
            }

            var nomad = _profiles.Find(p => p.Name == "Nomad");
            if (nomad != null)
            {
                nomad.PrimaryWeapons.Clear();
                nomad.SecondaryWeapons.Clear();
                nomad.PrimaryWeapons.Add(new WeaponInfo("AK-74M", "W_AK74M.png"));
                nomad.PrimaryWeapons.Add(new WeaponInfo("ARX200", "W_ARX200.png"));
                nomad.SecondaryWeapons.Add(new WeaponInfo("PRB92", "W_PRB92.png"));
                nomad.SecondaryWeapons.Add(new WeaponInfo(".44 Mag Semi-Auto", "W_44MAGSEMIAUTO.png"));
                nomad.SelectedPrimaryIndex = 0;
                nomad.SelectedSecondaryIndex = 0;
            }

            var gridlock = _profiles.Find(p => p.Name == "Gridlock");
            if (gridlock != null)
            {
                gridlock.PrimaryWeapons.Clear();
                gridlock.SecondaryWeapons.Clear();
                gridlock.PrimaryWeapons.Add(new WeaponInfo("F90", "W_F90.png"));
                gridlock.PrimaryWeapons.Add(new WeaponInfo("M249 SAW", "W_M249SAW.png"));
                gridlock.SecondaryWeapons.Add(new WeaponInfo("Super Shorty", "W_SUPERSHORTY.png"));
                gridlock.SecondaryWeapons.Add(new WeaponInfo("SDP 9mm", "W_SDP9MM.png"));
                gridlock.SelectedPrimaryIndex = 0;
                gridlock.SelectedSecondaryIndex = 0;
            }

            var nokk = _profiles.Find(p => p.Name == "Nøkk");
            if (nokk != null)
            {
                nokk.PrimaryWeapons.Clear();
                nokk.SecondaryWeapons.Clear();
                nokk.PrimaryWeapons.Add(new WeaponInfo("FMG-9", "W_FMG9.png"));
                nokk.PrimaryWeapons.Add(new WeaponInfo("SIX12 SD", "W_SIX12SD.png"));
                nokk.SecondaryWeapons.Add(new WeaponInfo("5.7 USG", "W_57USG.png"));
                nokk.SecondaryWeapons.Add(new WeaponInfo("D-50", "W_D50.png"));
                nokk.SelectedPrimaryIndex = 0;
                nokk.SelectedSecondaryIndex = 0;
            }

            var amaru = _profiles.Find(p => p.Name == "Amaru");
            if (amaru != null)
            {
                amaru.PrimaryWeapons.Clear();
                amaru.SecondaryWeapons.Clear();
                amaru.PrimaryWeapons.Add(new WeaponInfo("G8A1", "W_G8A1.png"));
                amaru.PrimaryWeapons.Add(new WeaponInfo("SuperNova", "W_SUPERNOVA.png"));
                amaru.SecondaryWeapons.Add(new WeaponInfo("SMG-11", "W_SMG11.png"));
                amaru.SecondaryWeapons.Add(new WeaponInfo("GONNE-6", "W_GONNE6.png"));
                amaru.SecondaryWeapons.Add(new WeaponInfo("ITA12S", "W_ITA12S.png"));
                amaru.SelectedPrimaryIndex = 0;
                amaru.SelectedSecondaryIndex = 0;
            }

            var kali = _profiles.Find(p => p.Name == "Kali");
            if (kali != null)
            {
                kali.PrimaryWeapons.Clear();
                kali.SecondaryWeapons.Clear();
                kali.PrimaryWeapons.Add(new WeaponInfo("CSRX 300", "W_CSRX300.png"));
                kali.SecondaryWeapons.Add(new WeaponInfo("SPSMG9", "W_SPSMG9.png"));
                kali.SecondaryWeapons.Add(new WeaponInfo("C75 Auto", "W_C75AUTO.png"));
                kali.SecondaryWeapons.Add(new WeaponInfo("P226 Mk 25", "W_P226MK25.png"));
                kali.SelectedPrimaryIndex = 0;
                kali.SelectedSecondaryIndex = 0;
            }

            var iana = _profiles.Find(p => p.Name == "Iana");
            if (iana != null)
            {
                iana.PrimaryWeapons.Clear();
                iana.SecondaryWeapons.Clear();
                iana.PrimaryWeapons.Add(new WeaponInfo("ARX200", "W_ARX200.png"));
                iana.PrimaryWeapons.Add(new WeaponInfo("G36C", "W_G36C.png"));
                iana.SecondaryWeapons.Add(new WeaponInfo("Mk1 9mm", "W_MK19MM.png"));
                iana.SecondaryWeapons.Add(new WeaponInfo("GONNE-6", "W_GONNE6.png"));
                iana.SelectedPrimaryIndex = 0;
                iana.SelectedSecondaryIndex = 0;
            }

            var ace = _profiles.Find(p => p.Name == "Ace");
            if (ace != null)
            {
                ace.PrimaryWeapons.Clear();
                ace.SecondaryWeapons.Clear();
                ace.PrimaryWeapons.Add(new WeaponInfo("AK-12", "W_AK12.png"));
                ace.PrimaryWeapons.Add(new WeaponInfo("M1014", "W_M1014.png"));
                ace.SecondaryWeapons.Add(new WeaponInfo("P9", "W_P9.png"));
                ace.SelectedPrimaryIndex = 0;
                ace.SelectedSecondaryIndex = 0;
            }

            var zero = _profiles.Find(p => p.Name == "Zero");
            if (zero != null)
            {
                zero.PrimaryWeapons.Clear();
                zero.SecondaryWeapons.Clear();
                zero.PrimaryWeapons.Add(new WeaponInfo("SC3000K", "W_SC3000K.png"));
                zero.PrimaryWeapons.Add(new WeaponInfo("MP7", "W_MP7.png"));
                zero.SecondaryWeapons.Add(new WeaponInfo("5.7 USG", "W_57USG.png"));
                zero.SecondaryWeapons.Add(new WeaponInfo("GONNE-6", "W_GONNE6.png"));
                zero.SelectedPrimaryIndex = 0;
                zero.SelectedSecondaryIndex = 0;
            }

            var flores = _profiles.Find(p => p.Name == "Flores");
            if (flores != null)
            {
                flores.PrimaryWeapons.Clear();
                flores.SecondaryWeapons.Clear();
                flores.PrimaryWeapons.Add(new WeaponInfo("AR33", "W_AR33.png"));
                flores.PrimaryWeapons.Add(new WeaponInfo("SR-25", "W_SR25.png"));
                flores.SecondaryWeapons.Add(new WeaponInfo("GSh-18", "W_GSH18.png"));
                flores.SelectedPrimaryIndex = 0;
                flores.SelectedSecondaryIndex = 0;
            }

            var osa = _profiles.Find(p => p.Name == "Osa");
            if (osa != null)
            {
                osa.PrimaryWeapons.Clear();
                osa.SecondaryWeapons.Clear();
                osa.PrimaryWeapons.Add(new WeaponInfo("556xi", "W_556XI.png"));
                osa.PrimaryWeapons.Add(new WeaponInfo("PDW9", "W_PDW9.png"));
                osa.SecondaryWeapons.Add(new WeaponInfo("PMM", "W_PMM.png"));
                osa.SelectedPrimaryIndex = 0;
                osa.SelectedSecondaryIndex = 0;
            }

            var sens = _profiles.Find(p => p.Name == "Sens");
            if (sens != null)
            {
                sens.PrimaryWeapons.Clear();
                sens.SecondaryWeapons.Clear();
                sens.PrimaryWeapons.Add(new WeaponInfo("POF-9", "W_POF9.png"));
                sens.PrimaryWeapons.Add(new WeaponInfo("417", "W_417.png"));
                sens.SecondaryWeapons.Add(new WeaponInfo("SDP 9mm", "W_SDP9MM.png"));
                sens.SelectedPrimaryIndex = 0;
                sens.SelectedSecondaryIndex = 0;
            }

            var grim = _profiles.Find(p => p.Name == "Grim");
            if (grim != null)
            {
                grim.PrimaryWeapons.Clear();
                grim.SecondaryWeapons.Clear();
                grim.PrimaryWeapons.Add(new WeaponInfo("552 Commando", "W_552COMMANDO.png"));
                grim.PrimaryWeapons.Add(new WeaponInfo("SG-CQB", "W_SGCQB.png"));
                grim.SecondaryWeapons.Add(new WeaponInfo("P229", "W_P229.png"));
                grim.SecondaryWeapons.Add(new WeaponInfo("Bailiff 410", "W_BAILIFF410.png"));
                grim.SelectedPrimaryIndex = 0;
                grim.SelectedSecondaryIndex = 0;
            }

            var brava = _profiles.Find(p => p.Name == "Brava");
            if (brava != null)
            {
                brava.PrimaryWeapons.Clear();
                brava.SecondaryWeapons.Clear();
                brava.PrimaryWeapons.Add(new WeaponInfo("PARA-308", "W_PARA308.png"));
                brava.PrimaryWeapons.Add(new WeaponInfo("CAMRS", "W_CAMRS.png"));
                brava.SecondaryWeapons.Add(new WeaponInfo("USP40", "W_USP40.png"));
                brava.SecondaryWeapons.Add(new WeaponInfo("Super Shorty", "W_SUPERSHORTY.png"));
                brava.SelectedPrimaryIndex = 0;
                brava.SelectedSecondaryIndex = 0;
            }

            var ram = _profiles.Find(p => p.Name == "Ram");
            if (ram != null)
            {
                ram.PrimaryWeapons.Clear();
                ram.SecondaryWeapons.Clear();
                ram.PrimaryWeapons.Add(new WeaponInfo("LMG-E", "W_LMGE.png"));
                ram.PrimaryWeapons.Add(new WeaponInfo("R4-C", "W_R4C.png"));
                ram.SecondaryWeapons.Add(new WeaponInfo("Mk1 9mm", "W_MK19MM.png"));
                ram.SelectedPrimaryIndex = 0;
                ram.SelectedSecondaryIndex = 0;
            }

            var deimos = _profiles.Find(p => p.Name == "Deimos");
            if (deimos != null)
            {
                deimos.PrimaryWeapons.Clear();
                deimos.SecondaryWeapons.Clear();
                deimos.PrimaryWeapons.Add(new WeaponInfo("AK-74M", "W_AK74M.png"));
                deimos.PrimaryWeapons.Add(new WeaponInfo("M590A1", "W_M590A1.png"));
                deimos.SecondaryWeapons.Add(new WeaponInfo(".44 Vendetta", "W_44VENDETTA.png"));
                deimos.SelectedPrimaryIndex = 0;
                deimos.SelectedSecondaryIndex = 0;
            }

            var rauora = _profiles.Find(p => p.Name == "Rauora");
            if (rauora != null)
            {
                rauora.PrimaryWeapons.Clear();
                rauora.SecondaryWeapons.Clear();
                rauora.PrimaryWeapons.Add(new WeaponInfo("417", "W_417.png"));
                rauora.PrimaryWeapons.Add(new WeaponInfo("M249", "W_M249.png"));
                rauora.SecondaryWeapons.Add(new WeaponInfo("Reaper MK2", "W_REAPERMK2.png"));
                rauora.SecondaryWeapons.Add(new WeaponInfo("GSh-18", "W_GSH18.png"));
                rauora.SelectedPrimaryIndex = 0;
                rauora.SelectedSecondaryIndex = 0;
            }

            // DEFENDERS
            var sentry = _profiles.Find(p => p.Name == "Sentry");
            if (sentry != null)
            {
                sentry.PrimaryWeapons.Clear();
                sentry.SecondaryWeapons.Clear();
                sentry.PrimaryWeapons.Add(new WeaponInfo("Commando 9", "W_COMMANDO9.png"));
                sentry.PrimaryWeapons.Add(new WeaponInfo("M870", "W_M870.png"));
                sentry.PrimaryWeapons.Add(new WeaponInfo("TCSG12", "W_TCSG12.png"));
                sentry.SecondaryWeapons.Add(new WeaponInfo("C75 Auto", "W_C75AUTO.png"));
                sentry.SecondaryWeapons.Add(new WeaponInfo("Super Shorty", "W_SUPERSHORTY.png"));
                sentry.SelectedPrimaryIndex = 0;
                sentry.SelectedSecondaryIndex = 0;
            }

            var smoke = _profiles.Find(p => p.Name == "Smoke");
            if (smoke != null)
            {
                smoke.PrimaryWeapons.Clear();
                smoke.SecondaryWeapons.Clear();
                smoke.PrimaryWeapons.Add(new WeaponInfo("M590A1", "W_M590A1.png"));
                smoke.PrimaryWeapons.Add(new WeaponInfo("FMG-9", "W_FMG9.png"));
                smoke.SecondaryWeapons.Add(new WeaponInfo("P226 Mk 25", "W_P226MK25.png"));
                smoke.SecondaryWeapons.Add(new WeaponInfo("SMG-11", "W_SMG11.png"));
                smoke.SelectedPrimaryIndex = 0;
                smoke.SelectedSecondaryIndex = 0;
            }

            var mute = _profiles.Find(p => p.Name == "Mute");
            if (mute != null)
            {
                mute.PrimaryWeapons.Clear();
                mute.SecondaryWeapons.Clear();
                mute.PrimaryWeapons.Add(new WeaponInfo("MP5K", "W_MP5K.png"));
                mute.PrimaryWeapons.Add(new WeaponInfo("M590A1", "W_M590A1.png"));
                mute.SecondaryWeapons.Add(new WeaponInfo("P226 Mk 25", "W_P226MK25.png"));
                mute.SecondaryWeapons.Add(new WeaponInfo("SMG-11", "W_SMG11.png"));
                mute.SelectedPrimaryIndex = 0;
                mute.SelectedSecondaryIndex = 0;
            }

            var castle = _profiles.Find(p => p.Name == "Castle");
            if (castle != null)
            {
                castle.PrimaryWeapons.Clear();
                castle.SecondaryWeapons.Clear();
                castle.PrimaryWeapons.Add(new WeaponInfo("UMP45", "W_UMP45.png"));
                castle.PrimaryWeapons.Add(new WeaponInfo("M1014", "W_M1014.png"));
                castle.SecondaryWeapons.Add(new WeaponInfo("5.7 USG", "W_57USG.png"));
                castle.SecondaryWeapons.Add(new WeaponInfo("Super Shorty", "W_SUPERSHORTY.png"));
                castle.SecondaryWeapons.Add(new WeaponInfo("M45 MEUSOC", "W_M45MEUSOC.png"));
                castle.SelectedPrimaryIndex = 0;
                castle.SelectedSecondaryIndex = 0;
            }

            var pulse = _profiles.Find(p => p.Name == "Pulse");
            if (pulse != null)
            {
                pulse.PrimaryWeapons.Clear();
                pulse.SecondaryWeapons.Clear();
                pulse.PrimaryWeapons.Add(new WeaponInfo("UMP45", "W_UMP45.png"));
                pulse.PrimaryWeapons.Add(new WeaponInfo("M1014", "W_M1014.png"));
                pulse.SecondaryWeapons.Add(new WeaponInfo("5.7 USG", "W_57USG.png"));
                pulse.SecondaryWeapons.Add(new WeaponInfo("M45 MEUSOC", "W_M45MEUSOC.png"));
                pulse.SecondaryWeapons.Add(new WeaponInfo("Reaper MK2", "W_REAPERMK2.png"));
                pulse.SelectedPrimaryIndex = 0;
                pulse.SelectedSecondaryIndex = 0;
            }

            var doc = _profiles.Find(p => p.Name == "Doc");
            if (doc != null)
            {
                doc.PrimaryWeapons.Clear();
                doc.SecondaryWeapons.Clear();
                doc.PrimaryWeapons.Add(new WeaponInfo("MP5", "W_MP5.png"));
                doc.PrimaryWeapons.Add(new WeaponInfo("P90", "W_P90.png"));
                doc.PrimaryWeapons.Add(new WeaponInfo("SG-CQB", "W_SGCQB.png"));
                doc.SecondaryWeapons.Add(new WeaponInfo("P9", "W_P9.png"));
                doc.SecondaryWeapons.Add(new WeaponInfo("LFP586", "W_LFP586.png"));
                doc.SecondaryWeapons.Add(new WeaponInfo("Bailiff 410", "W_BAILIFF410.png"));
                doc.SelectedPrimaryIndex = 0;
                doc.SelectedSecondaryIndex = 0;
            }

            var rook = _profiles.Find(p => p.Name == "Rook");
            if (rook != null)
            {
                rook.PrimaryWeapons.Clear();
                rook.SecondaryWeapons.Clear();
                rook.PrimaryWeapons.Add(new WeaponInfo("MP5", "W_MP5.png"));
                rook.PrimaryWeapons.Add(new WeaponInfo("P90", "W_P90.png"));
                rook.PrimaryWeapons.Add(new WeaponInfo("SG-CQB", "W_SGCQB.png"));
                rook.SecondaryWeapons.Add(new WeaponInfo("P9", "W_P9.png"));
                rook.SecondaryWeapons.Add(new WeaponInfo("LFP586", "W_LFP586.png"));
                rook.SecondaryWeapons.Add(new WeaponInfo("Reaper MK2", "W_REAPERMK2.png"));
                rook.SelectedPrimaryIndex = 0;
                rook.SelectedSecondaryIndex = 0;
            }

            var kapkan = _profiles.Find(p => p.Name == "Kapkan");
            if (kapkan != null)
            {
                kapkan.PrimaryWeapons.Clear();
                kapkan.SecondaryWeapons.Clear();
                kapkan.PrimaryWeapons.Add(new WeaponInfo("9x19VSN", "W_9X19VSN.png"));
                kapkan.PrimaryWeapons.Add(new WeaponInfo("SASG-12", "W_SASG12.png"));
                kapkan.SecondaryWeapons.Add(new WeaponInfo("PMM", "W_PMM.png"));
                kapkan.SecondaryWeapons.Add(new WeaponInfo("GSh-18", "W_GSH18.png"));
                kapkan.SelectedPrimaryIndex = 0;
                kapkan.SelectedSecondaryIndex = 0;
            }

            var tachanka = _profiles.Find(p => p.Name == "Tachanka");
            if (tachanka != null)
            {
                tachanka.PrimaryWeapons.Clear();
                tachanka.SecondaryWeapons.Clear();
                tachanka.PrimaryWeapons.Add(new WeaponInfo("DP27 (LMG)", "W_DP27LMG.png"));
                tachanka.PrimaryWeapons.Add(new WeaponInfo("9x19VSN", "W_9X19VSN.png"));
                tachanka.SecondaryWeapons.Add(new WeaponInfo("PMM", "W_PMM.png"));
                tachanka.SecondaryWeapons.Add(new WeaponInfo("GSh-18", "W_GSH18.png"));
                tachanka.SecondaryWeapons.Add(new WeaponInfo("Bearing 9", "W_BEARING9.png"));
                tachanka.SelectedPrimaryIndex = 0;
                tachanka.SelectedSecondaryIndex = 0;
            }

            var jager = _profiles.Find(p => p.Name == "Jäger");
            if (jager != null)
            {
                jager.PrimaryWeapons.Clear();
                jager.SecondaryWeapons.Clear();
                jager.PrimaryWeapons.Add(new WeaponInfo("416-C Carbine", "W_416CCARBINE.png"));
                jager.PrimaryWeapons.Add(new WeaponInfo("M870", "W_M870.png"));
                jager.SecondaryWeapons.Add(new WeaponInfo("P12", "W_P12.png"));
                jager.SelectedPrimaryIndex = 0;
                jager.SelectedSecondaryIndex = 0;
            }

            var bandit = _profiles.Find(p => p.Name == "Bandit");
            if (bandit != null)
            {
                bandit.PrimaryWeapons.Clear();
                bandit.SecondaryWeapons.Clear();
                bandit.PrimaryWeapons.Add(new WeaponInfo("MP7", "W_MP7.png"));
                bandit.PrimaryWeapons.Add(new WeaponInfo("M870", "W_M870.png"));
                bandit.SecondaryWeapons.Add(new WeaponInfo("P12", "W_P12.png"));
                bandit.SelectedPrimaryIndex = 0;
                bandit.SelectedSecondaryIndex = 0;
            }

            var frost = _profiles.Find(p => p.Name == "Frost");
            if (frost != null)
            {
                frost.PrimaryWeapons.Clear();
                frost.SecondaryWeapons.Clear();
                frost.PrimaryWeapons.Add(new WeaponInfo("9mm C1", "W_9MMC1.png"));
                frost.PrimaryWeapons.Add(new WeaponInfo("Super 90", "W_SUPER90.png"));
                frost.SecondaryWeapons.Add(new WeaponInfo("Mk1 9mm", "W_MK19MM.png"));
                frost.SecondaryWeapons.Add(new WeaponInfo("ITA12S", "W_ITA12S.png"));
                frost.SelectedPrimaryIndex = 0;
                frost.SelectedSecondaryIndex = 0;
            }

            var valkyrie = _profiles.Find(p => p.Name == "Valkyrie");
            if (valkyrie != null)
            {
                valkyrie.PrimaryWeapons.Clear();
                valkyrie.SecondaryWeapons.Clear();
                valkyrie.PrimaryWeapons.Add(new WeaponInfo("MPX", "W_MPX.png"));
                valkyrie.PrimaryWeapons.Add(new WeaponInfo("SPAS-12", "W_SPAS12.png"));
                valkyrie.SecondaryWeapons.Add(new WeaponInfo("D-50", "W_D50.png"));
                valkyrie.SelectedPrimaryIndex = 0;
                valkyrie.SelectedSecondaryIndex = 0;
            }

            var caveira = _profiles.Find(p => p.Name == "Caveira");
            if (caveira != null)
            {
                caveira.PrimaryWeapons.Clear();
                caveira.SecondaryWeapons.Clear();
                caveira.PrimaryWeapons.Add(new WeaponInfo("M12", "W_M12.png"));
                caveira.PrimaryWeapons.Add(new WeaponInfo("SPAS-15", "W_SPAS15.png"));
                caveira.SecondaryWeapons.Add(new WeaponInfo("Luison", "W_LUISON.png"));
                caveira.SelectedPrimaryIndex = 0;
                caveira.SelectedSecondaryIndex = 0;
            }

            var echo = _profiles.Find(p => p.Name == "Echo");
            if (echo != null)
            {
                echo.PrimaryWeapons.Clear();
                echo.SecondaryWeapons.Clear();
                echo.PrimaryWeapons.Add(new WeaponInfo("MP5SD", "W_MP5SD.png"));
                echo.PrimaryWeapons.Add(new WeaponInfo("SuperNova", "W_SUPERNOVA.png"));
                echo.SecondaryWeapons.Add(new WeaponInfo("P229", "W_P229.png"));
                echo.SecondaryWeapons.Add(new WeaponInfo("Bearing 9", "W_BEARING9.png"));
                echo.SelectedPrimaryIndex = 0;
                echo.SelectedSecondaryIndex = 0;
            }

            var mira = _profiles.Find(p => p.Name == "Mira");
            if (mira != null)
            {
                mira.PrimaryWeapons.Clear();
                mira.SecondaryWeapons.Clear();
                mira.PrimaryWeapons.Add(new WeaponInfo("Vector .45 ACP", "W_VECTOR45ACP.png"));
                mira.PrimaryWeapons.Add(new WeaponInfo("ITA12L", "W_ITA12L.png"));
                mira.SecondaryWeapons.Add(new WeaponInfo("ITA12S", "W_ITA12S.png"));
                mira.SecondaryWeapons.Add(new WeaponInfo("USP40", "W_USP40.png"));
                mira.SelectedPrimaryIndex = 0;
                mira.SelectedSecondaryIndex = 0;
            }

            var lesion = _profiles.Find(p => p.Name == "Lesion");
            if (lesion != null)
            {
                lesion.PrimaryWeapons.Clear();
                lesion.SecondaryWeapons.Clear();
                lesion.PrimaryWeapons.Add(new WeaponInfo("T-5 SMG", "W_T5SMG.png"));
                lesion.PrimaryWeapons.Add(new WeaponInfo("SIX12", "W_SIX12.png"));
                lesion.SecondaryWeapons.Add(new WeaponInfo("Q-929", "W_Q929.png"));
                lesion.SecondaryWeapons.Add(new WeaponInfo("Super Shorty", "W_SUPERSHORTY.png"));
                lesion.SelectedPrimaryIndex = 0;
                lesion.SelectedSecondaryIndex = 0;
            }

            var ela = _profiles.Find(p => p.Name == "Ela");
            if (ela != null)
            {
                ela.PrimaryWeapons.Clear();
                ela.SecondaryWeapons.Clear();
                ela.PrimaryWeapons.Add(new WeaponInfo("Scorpion EVO 3 A1", "W_SCORPIONEVO3A1.png"));
                ela.PrimaryWeapons.Add(new WeaponInfo("FO-12", "W_FO12.png"));
                ela.SecondaryWeapons.Add(new WeaponInfo("RG15", "W_RG15.png"));
                ela.SelectedPrimaryIndex = 0;
                ela.SelectedSecondaryIndex = 0;
            }

            var vigil = _profiles.Find(p => p.Name == "Vigil");
            if (vigil != null)
            {
                vigil.PrimaryWeapons.Clear();
                vigil.SecondaryWeapons.Clear();
                vigil.PrimaryWeapons.Add(new WeaponInfo("K1A", "W_K1A.png"));
                vigil.PrimaryWeapons.Add(new WeaponInfo("BOSG.12.2", "W_BOSG122.png"));
                vigil.SecondaryWeapons.Add(new WeaponInfo("C75 Auto", "W_C75AUTO.png"));
                vigil.SecondaryWeapons.Add(new WeaponInfo("SMG-12", "W_SMG12.png"));
                vigil.SelectedPrimaryIndex = 0;
                vigil.SelectedSecondaryIndex = 0;
            }

            var alibi = _profiles.Find(p => p.Name == "Alibi");
            if (alibi != null)
            {
                alibi.PrimaryWeapons.Clear();
                alibi.SecondaryWeapons.Clear();
                alibi.PrimaryWeapons.Add(new WeaponInfo("Mx4 Storm", "W_MX4STORM.png"));
                alibi.PrimaryWeapons.Add(new WeaponInfo("ACS12", "W_ACS12.png"));
                alibi.SecondaryWeapons.Add(new WeaponInfo("Keratos .357", "W_KERATOS357.png"));
                alibi.SecondaryWeapons.Add(new WeaponInfo("Bailiff 410", "W_BAILIFF410.png"));
                alibi.SelectedPrimaryIndex = 0;
                alibi.SelectedSecondaryIndex = 0;
            }

            var maestro = _profiles.Find(p => p.Name == "Maestro");
            if (maestro != null)
            {
                maestro.PrimaryWeapons.Clear();
                maestro.SecondaryWeapons.Clear();
                maestro.PrimaryWeapons.Add(new WeaponInfo("ALDA 5.56", "W_ALDA556.png"));
                maestro.PrimaryWeapons.Add(new WeaponInfo("ACS12", "W_ACS12.png"));
                maestro.SecondaryWeapons.Add(new WeaponInfo("Keratos .357", "W_KERATOS357.png"));
                maestro.SecondaryWeapons.Add(new WeaponInfo("Bailiff 410", "W_BAILIFF410.png"));
                maestro.SelectedPrimaryIndex = 0;
                maestro.SelectedSecondaryIndex = 0;
            }

            var clash = _profiles.Find(p => p.Name == "Clash");
            if (clash != null)
            {
                clash.PrimaryWeapons.Clear();
                clash.SecondaryWeapons.Clear();
                clash.PrimaryWeapons.Add(new WeaponInfo("CCE Shield", "W_CCESHIELD.png"));
                clash.SecondaryWeapons.Add(new WeaponInfo("P-10C", "W_P10C.png"));
                clash.SecondaryWeapons.Add(new WeaponInfo("SPSMG9", "W_SPSMG9.png"));
                clash.SecondaryWeapons.Add(new WeaponInfo("Super Shorty", "W_SUPERSHORTY.png"));
                clash.SelectedPrimaryIndex = 0;
                clash.SelectedSecondaryIndex = 0;
            }

            var kaid = _profiles.Find(p => p.Name == "Kaid");
            if (kaid != null)
            {
                kaid.PrimaryWeapons.Clear();
                kaid.SecondaryWeapons.Clear();
                kaid.PrimaryWeapons.Add(new WeaponInfo("AUG A3", "W_AUGA3.png"));
                kaid.PrimaryWeapons.Add(new WeaponInfo("TCSG12", "W_TCSG12.png"));
                kaid.SecondaryWeapons.Add(new WeaponInfo(".44 Mag Semi-Auto", "W_44MAGSEMIAUTO.png"));
                kaid.SecondaryWeapons.Add(new WeaponInfo("LFP586", "W_LFP586.png"));
                kaid.SelectedPrimaryIndex = 0;
                kaid.SelectedSecondaryIndex = 0;
            }

            var mozzie = _profiles.Find(p => p.Name == "Mozzie");
            if (mozzie != null)
            {
                mozzie.PrimaryWeapons.Clear();
                mozzie.SecondaryWeapons.Clear();
                mozzie.PrimaryWeapons.Add(new WeaponInfo("Commando 9", "W_COMMANDO9.png"));
                mozzie.PrimaryWeapons.Add(new WeaponInfo("P10 RONI", "W_P10RONI.png"));
                mozzie.SecondaryWeapons.Add(new WeaponInfo("SDP 9mm", "W_SDP9MM.png"));
                mozzie.SelectedPrimaryIndex = 0;
                mozzie.SelectedSecondaryIndex = 0;
            }

            var warden = _profiles.Find(p => p.Name == "Warden");
            if (warden != null)
            {
                warden.PrimaryWeapons.Clear();
                warden.SecondaryWeapons.Clear();
                warden.PrimaryWeapons.Add(new WeaponInfo("MPX", "W_MPX.png"));
                warden.PrimaryWeapons.Add(new WeaponInfo("M590A1", "W_M590A1.png"));
                warden.SecondaryWeapons.Add(new WeaponInfo("P-10C", "W_P10C.png"));
                warden.SecondaryWeapons.Add(new WeaponInfo("SMG-12", "W_SMG12.png"));
                warden.SelectedPrimaryIndex = 0;
                warden.SelectedSecondaryIndex = 0;
            }

            var goyo = _profiles.Find(p => p.Name == "Goyo");
            if (goyo != null)
            {
                goyo.PrimaryWeapons.Clear();
                goyo.SecondaryWeapons.Clear();
                goyo.PrimaryWeapons.Add(new WeaponInfo("Vector .45 ACP", "W_VECTOR45ACP.png"));
                goyo.PrimaryWeapons.Add(new WeaponInfo("TCSG12", "W_TCSG12.png"));
                goyo.SecondaryWeapons.Add(new WeaponInfo("P229", "W_P229.png"));
                goyo.SelectedPrimaryIndex = 0;
                goyo.SelectedSecondaryIndex = 0;
            }

            var wamai = _profiles.Find(p => p.Name == "Wamai");
            if (wamai != null)
            {
                wamai.PrimaryWeapons.Clear();
                wamai.SecondaryWeapons.Clear();
                wamai.PrimaryWeapons.Add(new WeaponInfo("AUG A2", "W_AUGA2.png"));
                wamai.PrimaryWeapons.Add(new WeaponInfo("MP5K", "W_MP5K.png"));
                wamai.SecondaryWeapons.Add(new WeaponInfo("Keratos .357", "W_KERATOS357.png"));
                wamai.SecondaryWeapons.Add(new WeaponInfo("P12", "W_P12.png"));
                wamai.SecondaryWeapons.Add(new WeaponInfo("Super Shorty", "W_SUPERSHORTY.png"));
                wamai.SelectedPrimaryIndex = 0;
                wamai.SelectedSecondaryIndex = 0;
            }

            var oryx = _profiles.Find(p => p.Name == "Oryx");
            if (oryx != null)
            {
                oryx.PrimaryWeapons.Clear();
                oryx.SecondaryWeapons.Clear();
                oryx.PrimaryWeapons.Add(new WeaponInfo("T-5 SMG", "W_T5SMG.png"));
                oryx.PrimaryWeapons.Add(new WeaponInfo("SPAS-12", "W_SPAS12.png"));
                oryx.SecondaryWeapons.Add(new WeaponInfo("Bailiff 410", "W_BAILIFF410.png"));
                oryx.SecondaryWeapons.Add(new WeaponInfo("USP40", "W_USP40.png"));
                oryx.SecondaryWeapons.Add(new WeaponInfo("Reaper MK2", "W_REAPERMK2.png"));
                oryx.SelectedPrimaryIndex = 0;
                oryx.SelectedSecondaryIndex = 0;
            }

            var melusi = _profiles.Find(p => p.Name == "Melusi");
            if (melusi != null)
            {
                melusi.PrimaryWeapons.Clear();
                melusi.SecondaryWeapons.Clear();
                melusi.PrimaryWeapons.Add(new WeaponInfo("MP5K", "W_MP5K.png"));
                melusi.PrimaryWeapons.Add(new WeaponInfo("Super 90", "W_SUPER90.png"));
                melusi.SecondaryWeapons.Add(new WeaponInfo("RG15", "W_RG15.png"));
                melusi.SecondaryWeapons.Add(new WeaponInfo("ITA12S", "W_ITA12S.png"));
                melusi.SelectedPrimaryIndex = 0;
                melusi.SelectedSecondaryIndex = 0;
            }

            var aruni = _profiles.Find(p => p.Name == "Aruni");
            if (aruni != null)
            {
                aruni.PrimaryWeapons.Clear();
                aruni.SecondaryWeapons.Clear();
                aruni.PrimaryWeapons.Add(new WeaponInfo("P10 RONI", "W_P10RONI.png"));
                aruni.PrimaryWeapons.Add(new WeaponInfo("MK14 EBR", "W_MK14EBR.png"));
                aruni.SecondaryWeapons.Add(new WeaponInfo("PRB92", "W_PRB92.png"));
                aruni.SelectedPrimaryIndex = 0;
                aruni.SelectedSecondaryIndex = 0;
            }

            var thunderbird = _profiles.Find(p => p.Name == "Thunderbird");
            if (thunderbird != null)
            {
                thunderbird.PrimaryWeapons.Clear();
                thunderbird.SecondaryWeapons.Clear();
                thunderbird.PrimaryWeapons.Add(new WeaponInfo("SPEAR .308", "W_SPEAR308.png"));
                thunderbird.PrimaryWeapons.Add(new WeaponInfo("SPAS-15", "W_SPAS15.png"));
                thunderbird.SecondaryWeapons.Add(new WeaponInfo("Q-929", "W_Q929.png"));
                thunderbird.SecondaryWeapons.Add(new WeaponInfo("Bearing 9", "W_BEARING9.png"));
                thunderbird.SecondaryWeapons.Add(new WeaponInfo("ITA12S", "W_ITA12S.png"));
                thunderbird.SelectedPrimaryIndex = 0;
                thunderbird.SelectedSecondaryIndex = 0;
            }

            var thorn = _profiles.Find(p => p.Name == "Thorn");
            if (thorn != null)
            {
                thorn.PrimaryWeapons.Clear();
                thorn.SecondaryWeapons.Clear();
                thorn.PrimaryWeapons.Add(new WeaponInfo("UZK50Gi", "W_UZK50GI.png"));
                thorn.PrimaryWeapons.Add(new WeaponInfo("M870", "W_M870.png"));
                thorn.SecondaryWeapons.Add(new WeaponInfo("1911 TACOPS", "W_1911TACOPS.png"));
                thorn.SecondaryWeapons.Add(new WeaponInfo("C75 Auto", "W_C75AUTO.png"));
                thorn.SelectedPrimaryIndex = 0;
                thorn.SelectedSecondaryIndex = 0;
            }

            var azami = _profiles.Find(p => p.Name == "Azami");
            if (azami != null)
            {
                azami.PrimaryWeapons.Clear();
                azami.SecondaryWeapons.Clear();
                azami.PrimaryWeapons.Add(new WeaponInfo("9X19SVN", "W_9X19SVN.png"));
                azami.PrimaryWeapons.Add(new WeaponInfo("ACS12", "W_ACS12.png"));
                azami.SecondaryWeapons.Add(new WeaponInfo("D-50", "W_D50.png"));
                azami.SelectedPrimaryIndex = 0;
                azami.SelectedSecondaryIndex = 0;
            }

            var solis = _profiles.Find(p => p.Name == "Solis");
            if (solis != null)
            {
                solis.PrimaryWeapons.Clear();
                solis.SecondaryWeapons.Clear();
                solis.PrimaryWeapons.Add(new WeaponInfo("P90", "W_P90.png"));
                solis.PrimaryWeapons.Add(new WeaponInfo("ITA12L", "W_ITA12L.png"));
                solis.SecondaryWeapons.Add(new WeaponInfo("SMG-11", "W_SMG11.png"));
                solis.SelectedPrimaryIndex = 0;
                solis.SelectedSecondaryIndex = 0;
            }

            var fenrir = _profiles.Find(p => p.Name == "Fenrir");
            if (fenrir != null)
            {
                fenrir.PrimaryWeapons.Clear();
                fenrir.SecondaryWeapons.Clear();
                fenrir.PrimaryWeapons.Add(new WeaponInfo("MP7", "W_MP7.png"));
                fenrir.PrimaryWeapons.Add(new WeaponInfo("SASG-12", "W_SASG12.png"));
                fenrir.SecondaryWeapons.Add(new WeaponInfo("5.7 USG", "W_57USG.png"));
                fenrir.SelectedPrimaryIndex = 0;
                fenrir.SelectedSecondaryIndex = 0;
            }

            var tubarao = _profiles.Find(p => p.Name == "Tubarão");
            if (tubarao != null)
            {
                tubarao.PrimaryWeapons.Clear();
                tubarao.SecondaryWeapons.Clear();
                tubarao.PrimaryWeapons.Add(new WeaponInfo("MPX", "W_MPX.png"));
                tubarao.PrimaryWeapons.Add(new WeaponInfo("AR-15.50", "W_AR1550.png"));
                tubarao.SecondaryWeapons.Add(new WeaponInfo("P226 Mk 25", "W_P226MK25.png"));
                tubarao.SelectedPrimaryIndex = 0;
                tubarao.SelectedSecondaryIndex = 0;
            }

            var skopos = _profiles.Find(p => p.Name == "Skopós");
            if (skopos != null)
            {
                skopos.PrimaryWeapons.Clear();
                skopos.SecondaryWeapons.Clear();
                skopos.PrimaryWeapons.Add(new WeaponInfo("PCX-33", "W_PCX33.png"));
                skopos.SecondaryWeapons.Add(new WeaponInfo("P229", "W_P229.png"));
                skopos.SelectedPrimaryIndex = 0;
                skopos.SelectedSecondaryIndex = 0;
            }

            var denari = _profiles.Find(p => p.Name == "Denari");
            if (denari != null)
            {
                denari.PrimaryWeapons.Clear();
                denari.SecondaryWeapons.Clear();
                denari.PrimaryWeapons.Add(new WeaponInfo("Scorpion EVO 3 A1", "W_SCORPIONEVO3A1.png"));
                denari.PrimaryWeapons.Add(new WeaponInfo("FMG-9", "W_FMG9.png"));
                denari.SecondaryWeapons.Add(new WeaponInfo("Glaive-12", "W_GLAIVE12.png"));
                denari.SecondaryWeapons.Add(new WeaponInfo("P226 Mk 25", "W_P226MK25.png"));
                denari.SelectedPrimaryIndex = 0;
                denari.SelectedSecondaryIndex = 0;
            }
        }


        // ==========================================================
        // Save / Load profiles to JSON (speeds + keys only)
        // ==========================================================
        private void SaveProfilesToFile()
        {
            try
            {
                var list = new List<ProfileData>();

                foreach (var p in _profiles)
                {
                    var data = new ProfileData
                    {
                        Category = p.Category,
                        Index = p.Index,

                        // old (still used)
                        Horizontal1 = p.Horizontal1,
                        Vertical1 = p.Vertical1,
                        Horizontal2 = p.Horizontal2,
                        Vertical2 = p.Vertical2,
                        Key1 = p.Key1,
                        Key2 = p.Key2,

                        // NEW: Setup 3
                        Horizontal3 = p.Horizontal3,
                        Vertical3 = p.Vertical3,
                        Key3 = p.Key3,

                        // NEW: which weapons are selected
                        SelectedPrimaryIndex = p.SelectedPrimaryIndex,
                        SelectedSecondaryIndex = p.SelectedSecondaryIndex
                    };


                    // --- Save recoil for each PRIMARY weapon ---
                    for (int i = 0; i < p.PrimaryWeapons.Count; i++)
                    {
                        var w = p.PrimaryWeapons[i];

                        data.Weapons.Add(new WeaponData
                        {
                            Slot = "Primary",
                            Index = i,
                            Horizontal = w.Horizontal,
                            Vertical = w.Vertical
                        });
                    }

                    // --- Save recoil for each SECONDARY weapon ---
                    for (int i = 0; i < p.SecondaryWeapons.Count; i++)
                    {
                        var w = p.SecondaryWeapons[i];

                        data.Weapons.Add(new WeaponData
                        {
                            Slot = "Secondary",
                            Index = i,
                            Horizontal = w.Horizontal,
                            Vertical = w.Vertical
                        });
                    }

                    list.Add(data);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(list, options);
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to save profiles:\n" + ex.Message,
                    "Save error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }


        private void LoadProfilesFromFile()
        {
            try
            {
                if (!File.Exists(_dataFilePath))
                    return;

                string json = File.ReadAllText(_dataFilePath);
                var list = JsonSerializer.Deserialize<List<ProfileData>>(json);
                if (list == null) return;

                foreach (var data in list)
                {
                    var p = _profiles.Find(p =>
                        p.Category == data.Category && p.Index == data.Index);
                    if (p == null) continue;

                    // --- Old fields (still important) ---
                    p.Horizontal1 = data.Horizontal1;
                    p.Vertical1 = data.Vertical1;
                    p.Horizontal2 = data.Horizontal2;
                    p.Vertical2 = data.Vertical2;
                    p.Key1 = data.Key1;
                    p.Key2 = data.Key2;

                    // NEW: Setup 3 (defaults will be 0 / None if old file)
                    p.Horizontal3 = data.Horizontal3;
                    p.Vertical3 = data.Vertical3;
                    p.Key3 = data.Key3;


                    // --- NEW: selected weapon indices ---
                    p.SelectedPrimaryIndex = data.SelectedPrimaryIndex;
                    p.SelectedSecondaryIndex = data.SelectedSecondaryIndex;

                    // --- NEW: set weapon recoil values ---
                    if (data.Weapons != null)
                    {
                        foreach (var wData in data.Weapons)
                        {
                            // choose the correct list for this slot
                            List<WeaponInfo> listRef =
                                string.Equals(wData.Slot, "Secondary", StringComparison.OrdinalIgnoreCase)
                                    ? p.SecondaryWeapons
                                    : p.PrimaryWeapons;

                            // make sure index is valid
                            if (wData.Index >= 0 && wData.Index < listRef.Count)
                            {
                                listRef[wData.Index].Horizontal = wData.Horizontal;
                                listRef[wData.Index].Vertical = wData.Vertical;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to load profiles:\n" + ex.Message,
                    "Load error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }


        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveProfilesToFile();
            ClearPictureBoxImage();

            if (_picturePrimaryWeapon?.Image != null)
            {
                _picturePrimaryWeapon.Image.Dispose();
                _picturePrimaryWeapon.Image = null;
            }

            if (_pictureSecondaryWeapon?.Image != null)
            {
                _pictureSecondaryWeapon.Image.Dispose();
                _pictureSecondaryWeapon.Image = null;
            }

            if (_appLogoImage != null)
            {
                _appLogoImage.Dispose();
                _appLogoImage = null;
            }
        }

        // ==========================================================
        // Category & profile CARDS
        // ==========================================================
        private void ShowCategory(string category)
        {
            _currentCategory = category;

            // reset highlight
            if (_selectedProfileCard != null)
            {
                _selectedProfileCard.BackColor = CardNormalColor;
                _selectedProfileCard = null;
            }

            _currentProfile = null;
            _labelSelectedProfile.Text = "Selected profile: (none)";
            _labelSelectedSetup.Text = "Setup: (none)";
            UpdateSetupSummaryLabels();

            if (_textKey1 != null) _textKey1.Text = "None";
            if (_textKey2 != null) _textKey2.Text = "None";
            _currentSetupIndex = 1;
            if (_labelActiveSetup != null) _labelActiveSetup.Text = "Active setup: 1 (Primary)";

            ClearPictureBoxImage();
            UpdateWeaponUi();

            StyleSegmentButton(_buttonCategoryA, category == "A");
            StyleSegmentButton(_buttonCategoryB, category == "B");

            // reset search placeholder when switching side
            if (_searchBox != null)
            {
                _searchHasPlaceholder = true;
                _searchBox.ForeColor = TextMuted;
                _searchBox.Text = SearchPlaceholder;
            }
            _currentSearchText = string.Empty;

            RefreshProfileCards();
        }

        private void RefreshProfileCards()
        {
            if (_profilesPanel == null)
                return;

            _profilesPanel.SuspendLayout();
            _profilesPanel.Controls.Clear();
            _profilesScrollOffset = 0;

            string search = _currentSearchText?.Trim() ?? string.Empty;
            bool hasSearch = search.Length > 0;

            Panel? existingSelectedCard = null;

            foreach (var p in _profiles)
            {
                if (p.Category != _currentCategory)
                    continue;

                if (hasSearch &&
                    (p.Name?.IndexOf(search, StringComparison.InvariantCultureIgnoreCase) ?? -1) < 0)
                    continue;

                var card = GetOrCreateProfileCard(p);
                _profilesPanel.Controls.Add(card);

                if (_currentProfile != null && ReferenceEquals(p, _currentProfile))
                {
                    existingSelectedCard = card;
                }
            }

            _profilesPanel.ResumeLayout();

            if (_profilesPanel.Controls.Count > 0)
            {
                if (_currentProfile == null || existingSelectedCard == null)
                {
                    if (_profilesPanel.Controls[0] is Panel firstCard &&
                        firstCard.Tag is Profile firstProfile)
                    {
                        SelectProfile(firstProfile, firstCard, goToSettings: false);
                    }
                }
                else
                {
                    HighlightSelectedCard(existingSelectedCard);
                }
            }
            else
            {
                _currentProfile = null;
                UpdateSelectedProfileDetails();
                UpdateWeaponUi();
            }

            CenterProfiles();
            UpdateProfilesScrollBar();
            UpdateActiveBadges();
        }

        private void SearchBox_GotFocus(object? sender, EventArgs e)
        {
            if (_searchHasPlaceholder)
            {
                _searchHasPlaceholder = false;
                _searchBox.Text = "";
                _searchBox.ForeColor = Color.White;
            }
        }

        private void SearchBox_LostFocus(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_searchBox.Text))
            {
                _searchHasPlaceholder = true;
                _searchBox.ForeColor = TextMuted;
                _searchBox.Text = SearchPlaceholder;
            }
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            if (_searchHasPlaceholder)
            {
                _currentSearchText = string.Empty;
            }
            else
            {
                _currentSearchText = _searchBox.Text ?? string.Empty;
            }
            RefreshProfileCards();
        }

        private Panel GetOrCreateProfileCard(Profile profile)
        {
            if (_profileCardCache.TryGetValue(profile, out var cachedCard))
            {
                return cachedCard;
            }

            var newCard = CreateProfileCard(profile);
            _profileCardCache[profile] = newCard;
            return newCard;
        }

        private void SyncToggleKeyUi()
        {
            if (_textToggleKey == null || _globalProfile == null)
                return;

            var key = _globalProfile.Key1;
            _textToggleKey.Text = key == Keys.None ? "None" : key.ToString();
        }


        private Panel CreateProfileCard(Profile profile)
        {
            var card = new Panel
            {
                Width = 170,
                Height = 190,
                Margin = new Padding(10),
                BackColor = CardNormalColor,
                Tag = profile,
                Cursor = Cursors.Hand
            };
            ApplyRoundedCorners(card, 8);

            var activeBadge = new Label
            {
                Name = ActiveBadgeName,
                AutoSize = false,
                Width = 60,
                Height = 18,
                Text = "ACTIVE",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                BackColor = AccentPositive,
                ForeColor = Color.White,
                Visible = false
            };
            ApplyRoundedCorners(activeBadge, 9);

            activeBadge.Location = new Point(
                (card.Width - activeBadge.Width) / 2,
                8
            );

            card.Resize += (s, e) =>
            {
                activeBadge.Location = new Point(
                    (card.Width - activeBadge.Width) / 2,
                    8
                );
            };

            var thumb = new PictureBox
            {
                Width = 150,
                Height = 100,
                Location = new Point(10, activeBadge.Bottom + 5),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(15, 23, 42)
            };
            LoadThumbnailImage(thumb, profile.ImageFileName);

            var nameLabel = new Label
            {
                AutoSize = false,
                Width = 150,
                Height = 18,
                Location = new Point(10, thumb.Bottom + 5),
                Text = profile.Name,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var modifyButton = new Button
            {
                Text = "Modify",
                Width = 120,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                Location = new Point(
                    (card.Width - 120) / 2,
                    nameLabel.Bottom + 5
                ),
                Cursor = Cursors.Hand
            };
            modifyButton.FlatAppearance.BorderSize = 0;
            modifyButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 85, 99);
            modifyButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(31, 41, 55);
            modifyButton.Click += (s, e) => StartModifyProfile(profile, card);

            card.Controls.Add(activeBadge);
            card.Controls.Add(thumb);
            card.Controls.Add(nameLabel);
            card.Controls.Add(modifyButton);

            card.MouseClick += ProfileCard_Click;
            thumb.MouseClick += ProfileCard_Click;
            nameLabel.MouseClick += ProfileCard_Click;

            void HandleEnter(object? s, EventArgs e)
            {
                if (card != _selectedProfileCard)
                    card.BackColor = CardHoverColor;
            }

            void HandleLeave(object? s, EventArgs e)
            {
                if (card != _selectedProfileCard)
                    card.BackColor = CardNormalColor;
            }

            foreach (Control c in new Control[] { card, thumb, nameLabel, modifyButton })
            {
                c.MouseEnter += HandleEnter;
                c.MouseLeave += HandleLeave;
            }

            return card;
        }

        private void ProfileCard_Click(object? sender, MouseEventArgs e)
        {
            Control? ctrl = sender as Control;
            if (ctrl == null) return;

            Panel? card = null;
            if (ctrl is Panel panel)
                card = panel;
            else if (ctrl.Parent is Panel parentPanel)
                card = parentPanel;

            if (card?.Tag is Profile profile)
            {
                SelectProfile(profile, card, goToSettings: false);
            }
        }

        private void StartModifyProfile(Profile profile, Panel card)
        {
            SelectProfile(profile, card, goToSettings: true);
        }

        private void SelectProfile(Profile profile, Panel? card, bool goToSettings)
        {
            // If we are staying on the profiles page and the search box is focused,
            // remove focus so you don't keep typing in it by mistake.
            if (!goToSettings && _searchBox != null && _searchBox.Focused)
            {
                ActiveControl = null; // drop focus from the search box
            }

            _currentProfile = profile;
            HighlightSelectedCard(card);
            LoadProfile(profile);
            UpdateSelectedProfileDetails();

            if (goToSettings)
            {
                ShowSettingsPage();
            }

            UpdateActiveBadges();
        }

        private void HighlightSelectedCard(Panel? card)
        {
            if (_selectedProfileCard != null)
            {
                _selectedProfileCard.BackColor = CardNormalColor;
            }

            _selectedProfileCard = card;

            if (card != null)
            {
                card.BackColor = CardSelectedColor;
            }
        }

        private void UpdateActiveBadges()
        {
            foreach (var kv in _profileCardCache)
            {
                var profile = kv.Key;
                var card = kv.Value;

                if (!card.Controls.ContainsKey(ActiveBadgeName))
                    continue;

                var badge = card.Controls[ActiveBadgeName] as Label;
                if (badge == null) continue;

                bool show = _isActive && _currentProfile != null && ReferenceEquals(profile, _currentProfile);
                badge.Visible = show;
            }
        }

        // show active setup + speeds in top bar
        private void UpdateSelectedProfileDetails()
        {
            if (_labelSelectedProfile == null || _labelSelectedSetup == null)
                return;

            if (_currentProfile == null)
            {
                _labelSelectedProfile.Text = "Selected profile: (none)";
                _labelSelectedSetup.Text = "Setup: (none)";
                LayoutProfilesTopBar();
                return;
            }

            _labelSelectedProfile.Text = $"Selected: {_currentProfile.Name}";

            string hText = _horizontalSpeed.ToString("0.000", CultureInfo.InvariantCulture);
            string vText = _verticalSpeed.ToString("0.000", CultureInfo.InvariantCulture);
            _labelSelectedSetup.Text = $"Setup: {_currentSetupIndex} (H: {hText}, V: {vText})";

            LayoutProfilesTopBar();
        }

        // Summary labels for saved setups
        private void UpdateSetupSummaryLabels()
        {
            if (_labelSetup1Summary == null || _labelSetup2Summary == null)
                return;

            if (_currentProfile == null)
            {
                _labelSetup1Summary.Text = "Primary: (no values)";
                _labelSetup2Summary.Text = "Secondary: (no values)";
                if (_labelSetup3Summary != null)
                    _labelSetup3Summary.Visible = false;
                return;
            }


            var primary = _currentProfile.SelectedPrimaryWeapon;
            var secondary = _currentProfile.SelectedSecondaryWeapon;

            // ---- Setup 1 (Primary) ----
            if (primary != null)
            {
                _labelSetup1Summary.Text =
                    $"PRIMARY ({primary.Name}) – H = {primary.Horizontal:0.000}, V = {primary.Vertical:0.000}";
            }
            else
            {
                _labelSetup1Summary.Text =
                    $"PRIMARY – H = {_currentProfile.Horizontal1:0.000}, V = {_currentProfile.Vertical1:0.000}";
            }

            // ---- Setup 2 (Secondary) ----
            if (secondary != null)
            {
                _labelSetup2Summary.Text =
                    $"SECONDARY ({secondary.Name}) – H = {secondary.Horizontal:0.000}, V = {secondary.Vertical:0.000}";
            }
            else
            {
                _labelSetup2Summary.Text =
                    $"SECONDARY – H = {_currentProfile.Horizontal2:0.000}, V = {_currentProfile.Vertical2:0.000}";
            }

            // NEW: Setup 3 summary (Maestro only, not tied to weapon)
            if (_labelSetup3Summary != null)
            {
                if (string.Equals(_currentProfile.Name, "Maestro", StringComparison.OrdinalIgnoreCase))
                {
                    _labelSetup3Summary.Visible = true;
                    _labelSetup3Summary.Text =
                        $"Setup 3 (Maestro): H = {_currentProfile.Horizontal3:0.000}, V = {_currentProfile.Vertical3:0.000}";
                }
                else
                {
                    _labelSetup3Summary.Visible = false;
                }
            }


        }


        private void LoadProfile(Profile profile)
        {
            _currentSetupIndex = 1;
            _labelActiveSetup.Text = "Active setup: 1 (Primary)";

            _textKey1.Text = profile.Key1 == Keys.None ? "None" : profile.Key1.ToString();
            _textKey2.Text = profile.Key2 == Keys.None ? "None" : profile.Key2.ToString();

            if (_textKey3 != null)
                _textKey3.Text = profile.Key3 == Keys.None ? "None" : profile.Key3.ToString();

            // Show/hide Setup 3 depending on Maestro
            UpdateSetup3Visibility();


            // make sure indexes are in range
            if (profile.PrimaryWeapons.Count > 0 &&
                profile.SelectedPrimaryIndex >= profile.PrimaryWeapons.Count)
                profile.SelectedPrimaryIndex = 0;

            if (profile.SecondaryWeapons.Count > 0 &&
                profile.SelectedSecondaryIndex >= profile.SecondaryWeapons.Count)
                profile.SelectedSecondaryIndex = 0;

            ApplyProfileSetup(profile, 1);
            UpdateWeaponUi();
            UpdateSetupSummaryLabels();
        }

        // ==========================================================
        // Reset helpers
        // ==========================================================
        private static void ResetProfileData(Profile profile)
        {
            profile.Horizontal1 = 0.0;
            profile.Vertical1 = 0.0;
            profile.Horizontal2 = 0.0;
            profile.Vertical2 = 0.0;
            profile.Key1 = Keys.None;
            profile.Key2 = Keys.None;

            // NEW
            profile.Horizontal3 = 0.0;
            profile.Vertical3 = 0.0;
            profile.Key3 = Keys.None;


            profile.SelectedPrimaryIndex = 0;
            profile.SelectedSecondaryIndex = 0;

            foreach (var w in profile.PrimaryWeapons)
            {
                w.Horizontal = 0.0;
                w.Vertical = 0.0;
            }

            foreach (var w in profile.SecondaryWeapons)
            {
                w.Horizontal = 0.0;
                w.Vertical = 0.0;
            }
        }

        // ==========================================================
        // Reset All (only speeds + keys)
        // ==========================================================
        private void ButtonResetAll_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This will reset ALL profiles' speeds and keybinds.\nNames and images stay.\nAre you sure?",
                "Reset all profiles",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            foreach (var p in _profiles)
            {
                ResetProfileData(p);
            }

            _horizontalSpeed = 0.0;
            _verticalSpeed = 0.0;
            _accumulatedX = 0.0;
            _accumulatedY = 0.0;

            _trackBarHorizontal.Value = 0;
            _trackBarVertical.Value = 0;
            UpdateHorizontalDisplay();
            UpdateVerticalDisplay();

            _currentSetupIndex = 1;
            _labelActiveSetup.Text = "Active setup: 1 (Primary)";
            _textKey1.Text = "None";
            _textKey2.Text = "None";
            if (_textKey3 != null) _textKey3.Text = "None";


            if (_currentProfile != null)
            {
                UpdateSelectedProfileDetails();
                UpdateSetupSummaryLabels();
            }

            UpdateWeaponUi();

            try
            {
                if (File.Exists(_dataFilePath))
                    File.Delete(_dataFilePath);
            }
            catch
            {
            }

            MessageBox.Show(
                "All speeds and keybinds have been reset.",
                "Reset complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            foreach (var p in _profiles)
            {
                ResetProfileData(p);
            }

            // ...

            // Make sure the tutorial Start / Stop textbox updates too
            SyncToggleKeyUi();


        }



        private void ButtonExportSettings_Click(object? sender, EventArgs e)
        {
            try
            {
                // Make sure the latest values are on disk
                SaveProfilesToFile();

                using (var dialog = new SaveFileDialog())
                {
                    dialog.Title = "Export settings";
                    dialog.Filter = "GM R6 settings (*.json)|*.json|All files (*.*)|*.*";
                    dialog.FileName = "GM_R6_Settings.json";

                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    // Copy current profiles.json to chosen file
                    File.Copy(_dataFilePath, dialog.FileName, true);

                    MessageBox.Show(
                        "Settings exported successfully.\nYou can share this .json file with your friends.",
                        "Export settings",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to export settings:\n" + ex.Message,
                    "Export error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ButtonImportSettings_Click(object? sender, EventArgs e)
        {
            try
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = "Import settings";
                    dialog.Filter = "GM R6 settings (*.json)|*.json|All files (*.*)|*.*";

                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    var confirm = MessageBox.Show(
                        "This will replace ALL your current speeds, keybinds and weapon recoil\n" +
                        "with the settings from the selected file.\n\nContinue?",
                        "Import settings",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (confirm != DialogResult.Yes)
                        return;

                    // Overwrite local profiles.json with the selected file
                    File.Copy(dialog.FileName, _dataFilePath, true);

                    // Reload profiles from the new file
                    LoadProfilesFromFile();

                    // Refresh UI (will re-select the first profile of current side)
                    ShowCategory(_currentCategory);

                    // 🔽 update Start/Stop key textbox from global profile
                    SyncToggleKeyUi();

                    MessageBox.Show(
                        "Settings imported successfully.",
                        "Import settings",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to import settings:\n" + ex.Message,
                    "Import error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        // Per-profile reset
        private void ButtonResetProfile_Click(object? sender, EventArgs e)
        {
            if (_currentProfile == null)
            {
                MessageBox.Show(
                    "No profile is selected.",
                    "Reset profile",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"This will reset speeds and keybinds for {_currentProfile.Name}.\nAre you sure?",
                "Reset this profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            ResetProfileData(_currentProfile);

            _currentSetupIndex = 1;
            _labelActiveSetup.Text = "Active setup: 1 (Primary)";

            ApplyProfileSetup(_currentProfile, 1);

            _textKey1.Text = "None";
            _textKey2.Text = "None";
            if (_textKey3 != null) _textKey3.Text = "None";


            UpdateSetupSummaryLabels();
            UpdateWeaponUi();
            SaveProfilesToFile();

            MessageBox.Show(
                $"Profile {_currentProfile.Name} has been reset.",
                "Profile reset",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ==========================================================
        // Image loading
        // ==========================================================
        private void LoadProfileImage(string? imageFileName)
        {
            ClearPictureBoxImage();

            if (string.IsNullOrWhiteSpace(imageFileName))
                return;

            try
            {
                string fullPath = Path.Combine(_imagesFolder, imageFileName);
                if (!File.Exists(fullPath))
                    return;

                using (var img = Image.FromFile(fullPath))
                {
                    _pictureProfile.Image = new Bitmap(img);
                }
            }
            catch
            {
            }
        }

        private void ButtonSaveSetup3_Click(object? sender, EventArgs e)
        {
            if (_currentProfile == null ||
                !string.Equals(_currentProfile.Name, "Maestro", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Setup 3 is only available for Maestro.",
                    "Setup 3",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            ApplyHorizontalText();
            ApplyVerticalText();

            _currentProfile.Horizontal3 = _horizontalSpeed;
            _currentProfile.Vertical3 = _verticalSpeed;

            ApplyProfileSetup(_currentProfile, 3);
            UpdateSetupSummaryLabels();
            SaveProfilesToFile();

            MessageBox.Show(
                $"Saved Setup 3 for {_currentProfile.Name}",
                "Setup 3 saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }


        private void LoadThumbnailImage(PictureBox target, string? imageFileName)
        {
            if (target.Image != null)
            {
                target.Image.Dispose();
                target.Image = null;
            }

            if (string.IsNullOrWhiteSpace(imageFileName))
                return;

            try
            {
                string fullPath = Path.Combine(_imagesFolder, imageFileName);
                if (!File.Exists(fullPath))
                    return;

                using (var img = Image.FromFile(fullPath))
                {
                    target.Image = new Bitmap(img);
                }
            }
            catch
            {
            }
        }

        private void ClearPictureBoxImage()
        {
            if (_pictureProfile?.Image != null)
            {
                _pictureProfile.Image.Dispose();
                _pictureProfile.Image = null;
            }
        }

        private void LoadWeaponImage(PictureBox? pictureBox, string? fileName)
        {
            if (pictureBox == null)
                return;

            if (pictureBox.Image != null)
            {
                pictureBox.Image.Dispose();
                pictureBox.Image = null;
            }

            if (string.IsNullOrWhiteSpace(fileName))
                return;

            try
            {
                string fullPath = Path.Combine(_imagesFolder, fileName);
                if (!File.Exists(fullPath))
                    return;

                using (var img = Image.FromFile(fullPath))
                {
                    pictureBox.Image = new Bitmap(img);
                }
            }
            catch
            {
                // ignore load errors – just leave it empty
            }
        }

        // ==========================================================
        // Key capture
        // ==========================================================
        private void StartCapturingKey(int setupIndex)
        {
            if (_currentProfile == null)
            {
                MessageBox.Show(
                    "Please select a profile first.",
                    "No profile selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _capturingKeyForSetup = setupIndex;

            if (setupIndex == 1)
                _textKey1.Text = "Press key...";
            else if (setupIndex == 2)
                _textKey2.Text = "Press key...";
            else if (setupIndex == 3 && _textKey3 != null)
                _textKey3.Text = "Press key...";

        }

        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            // 🔽 First: capture the global toggle key if requested
            if (_capturingToggleKey)
            {
                if (_globalProfile != null)
                {
                    _globalProfile.Key1 = e.KeyCode;
                }

                if (_textToggleKey != null)
                {
                    _textToggleKey.Text = e.KeyCode.ToString();
                }

                _capturingToggleKey = false;

                // persist it so it survives restart / export
                SaveProfilesToFile();

                e.Handled = true;
                return;
            }

            // Existing logic for setups 1/2/3
            if (_capturingKeyForSetup == 0 || _currentProfile == null)
                return;

            Keys key = e.KeyCode;

            if (_capturingKeyForSetup == 1)
            {
                _currentProfile.Key1 = key;
                _textKey1.Text = key.ToString();
            }
            else if (_capturingKeyForSetup == 2)
            {
                _currentProfile.Key2 = key;
                _textKey2.Text = key.ToString();
            }
            else if (_capturingKeyForSetup == 3)
            {
                _currentProfile.Key3 = key;
                if (_textKey3 != null)
                    _textKey3.Text = key.ToString();
            }

            _capturingKeyForSetup = 0;
            e.Handled = true;
        }


        // ==========================================================
        // Save setups for current profile
        // ==========================================================
        private void ButtonSaveSetup1_Click(object? sender, EventArgs e)
        {
            if (_currentProfile == null)
            {
                MessageBox.Show(
                    "Please select a profile first.",
                    "No profile selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ApplyHorizontalText();
            ApplyVerticalText();

            _currentProfile.Horizontal1 = _horizontalSpeed;
            _currentProfile.Vertical1 = _verticalSpeed;

            var primary = _currentProfile.SelectedPrimaryWeapon;
            if (primary != null)
            {
                primary.Horizontal = _horizontalSpeed;
                primary.Vertical = _verticalSpeed;
            }

            ApplyProfileSetup(_currentProfile, 1);
            UpdateSetupSummaryLabels();

            // ⬇️ add this line:
            SaveProfilesToFile();

            MessageBox.Show(
                $"Saved Setup 1 for {_currentProfile.Name}",
                "Setup 1 saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ButtonSaveSetup2_Click(object? sender, EventArgs e)
        {
            if (_currentProfile == null)
            {
                MessageBox.Show(
                    "Please select a profile first.",
                    "No profile selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ApplyHorizontalText();
            ApplyVerticalText();

            _currentProfile.Horizontal2 = _horizontalSpeed;
            _currentProfile.Vertical2 = _verticalSpeed;

            var secondary = _currentProfile.SelectedSecondaryWeapon;
            if (secondary != null)
            {
                secondary.Horizontal = _horizontalSpeed;
                secondary.Vertical = _verticalSpeed;
            }

            ApplyProfileSetup(_currentProfile, 2);
            UpdateSetupSummaryLabels();

            // ⬇️ add this line:
            SaveProfilesToFile();

            MessageBox.Show(
                $"Saved Setup 2 for {_currentProfile.Name}",
                "Setup 2 saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ApplyProfileSetup(Profile profile, int setupIndex)
        {
            _currentSetupIndex = setupIndex;

            string activeLabel;
            if (setupIndex == 1)
                activeLabel = "Active setup: 1 (Primary)";
            else if (setupIndex == 2)
                activeLabel = "Active setup: 2 (Secondary)";
            else
                activeLabel = "Active setup: 3 (Maestro)";

            _labelActiveSetup.Text = activeLabel;


            double h;
            double v;
            WeaponInfo? weapon = null;

            if (setupIndex == 1)
            {
                h = profile.Horizontal1;
                v = profile.Vertical1;

                weapon = profile.SelectedPrimaryWeapon;
                if (weapon != null && (weapon.Horizontal != 0.0 || weapon.Vertical != 0.0))
                {
                    h = weapon.Horizontal;
                    v = weapon.Vertical;
                }
            }
            else if (setupIndex == 2)
            {
                h = profile.Horizontal2;
                v = profile.Vertical2;

                weapon = profile.SelectedSecondaryWeapon;
                if (weapon != null && (weapon.Horizontal != 0.0 || weapon.Vertical != 0.0))
                {
                    h = weapon.Horizontal;
                    v = weapon.Vertical;
                }
            }
            else // Setup 3 (Maestro-only, no weapon override)
            {
                h = profile.Horizontal3;
                v = profile.Vertical3;
            }


            _horizontalSpeed = h;
            _verticalSpeed = v;

            int hTrack = (int)Math.Round(h * SliderScale);
            if (hTrack < _trackBarHorizontal.Minimum) hTrack = _trackBarHorizontal.Minimum;
            if (hTrack > _trackBarHorizontal.Maximum) hTrack = _trackBarHorizontal.Maximum;
            _trackBarHorizontal.Value = hTrack;

            int vTrack = (int)Math.Round(v * SliderScale);
            if (vTrack < _trackBarVertical.Minimum) vTrack = _trackBarVertical.Minimum;
            if (vTrack > _trackBarVertical.Maximum) vTrack = _trackBarVertical.Maximum;
            _trackBarVertical.Value = vTrack;

            UpdateHorizontalDisplay();
            UpdateVerticalDisplay();

            if (_currentProfile != null && ReferenceEquals(profile, _currentProfile))
            {
                UpdateSelectedProfileDetails();
                UpdateWeaponUi();
            }
        }


        // ==========================================================
        // Weapon selection logic
        // ==========================================================
        private void ChangeWeaponSelection(bool isPrimary, int delta)
        {
            if (_currentProfile == null)
                return;

            var profile = _currentProfile;
            var list = isPrimary ? profile.PrimaryWeapons : profile.SecondaryWeapons;
            if (list.Count == 0)
                return;

            if (isPrimary)
            {
                int idx = profile.SelectedPrimaryIndex;
                idx = (idx + delta + list.Count) % list.Count;
                profile.SelectedPrimaryIndex = idx;

                if (_currentSetupIndex == 1)
                    ApplyProfileSetup(profile, 1);
            }
            else
            {
                int idx = profile.SelectedSecondaryIndex;
                idx = (idx + delta + list.Count) % list.Count;
                profile.SelectedSecondaryIndex = idx;

                if (_currentSetupIndex == 2)
                    ApplyProfileSetup(profile, 2);
            }

            UpdateWeaponUi();
            UpdateSetupSummaryLabels();
        }

        private void UpdateWeaponUi()
        {
            if (_labelPrimaryWeaponName == null || _labelSecondaryWeaponName == null)
                return;

            if (_currentProfile == null)
            {
                _labelPrimaryWeaponName.Text = "Primary weapon";
                _labelSecondaryWeaponName.Text = "Secondary weapon";
                LoadWeaponImage(_picturePrimaryWeapon, null);
                LoadWeaponImage(_pictureSecondaryWeapon, null);
                return;
            }

            var primary = _currentProfile.SelectedPrimaryWeapon;
            var secondary = _currentProfile.SelectedSecondaryWeapon;

            _labelPrimaryWeaponName.Text = primary?.Name ?? "Primary weapon";
            _labelSecondaryWeaponName.Text = secondary?.Name ?? "Secondary weapon";

            LoadWeaponImage(_picturePrimaryWeapon, primary?.ImageFileName);
            LoadWeaponImage(_pictureSecondaryWeapon, secondary?.ImageFileName);
        }

        // ==========================================================
        // Horizontal / Vertical sync & parsing
        // ==========================================================
        private void SyncHorizontalFromSlider()
        {
            _horizontalSpeed = _trackBarHorizontal.Value / SliderScale;
            UpdateHorizontalDisplay();

            if (_currentProfile != null)
                UpdateSelectedProfileDetails();
        }

        private void SyncVerticalFromSlider()
        {
            _verticalSpeed = _trackBarVertical.Value / SliderScale;
            UpdateVerticalDisplay();

            if (_currentProfile != null)
                UpdateSelectedProfileDetails();
        }

        private void UpdateHorizontalDisplay()
        {
            string text = _horizontalSpeed.ToString("0.000", CultureInfo.InvariantCulture);
            _textHorizontal.Text = text;
        }

        private void UpdateVerticalDisplay()
        {
            string text = _verticalSpeed.ToString("0.000", CultureInfo.InvariantCulture);
            _textVertical.Text = text;
        }

        private void TrackBarHorizontal_Scroll(object? sender, EventArgs e)
        {
            SyncHorizontalFromSlider();
        }

        private void TrackBarVertical_Scroll(object? sender, EventArgs e)
        {
            SyncVerticalFromSlider();
        }

        private void TextHorizontal_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ApplyHorizontalText();
            }
        }

        private void TextHorizontal_Leave(object? sender, EventArgs e)
        {
            ApplyHorizontalText();
        }

        private void TextVertical_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ApplyVerticalText();
            }
        }

        private void TextVertical_Leave(object? sender, EventArgs e)
        {
            ApplyVerticalText();
        }

        private void ApplyHorizontalText()
        {
            if (double.TryParse(
                    _textHorizontal.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double value))
            {
                double min = _trackBarHorizontal.Minimum / SliderScale;
                double max = _trackBarHorizontal.Maximum / SliderScale;
                if (value < min) value = min;
                if (value > max) value = max;

                _horizontalSpeed = value;
                _trackBarHorizontal.Value = (int)Math.Round(value * SliderScale);
                UpdateHorizontalDisplay();

                if (_currentProfile != null)
                    UpdateSelectedProfileDetails();
            }
        }

        private void ApplyVerticalText()
        {
            if (double.TryParse(
                    _textVertical.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double value))
            {
                double min = _trackBarVertical.Minimum / SliderScale;
                double max = _trackBarVertical.Maximum / SliderScale;
                if (value < min) value = min;
                if (value > max) value = max;

                _verticalSpeed = value;
                _trackBarVertical.Value = (int)Math.Round(value * SliderScale);
                UpdateVerticalDisplay();

                if (_currentProfile != null)
                    UpdateSelectedProfileDetails();
            }
        }

        // ==========================================================
        // Start / stop & movement
        // ==========================================================
        private void ButtonStart_Click(object? sender, EventArgs e)
        {
            ToggleActive();
        }

        private void ToggleActive()
        {
            _isActive = !_isActive;

            if (_isActive)
            {
                _buttonStart.Text = "Stop";
                _buttonStart.BackColor = AccentDanger;
                BackColor = BgSettings;
            }
            else
            {
                _buttonStart.Text = "Start";
                _buttonStart.BackColor = AccentPrimary;
                BackColor = BgMain;
                _comboArmed = false;
                _comboActive = false;
            }

            UpdateActiveBadges();
        }


        private void CheckToggleKey(Keys key, ref bool wasDown)
        {
            if (key == Keys.None)
                return;

            bool isDown = IsKeyPressed(key);

            if (isDown && !wasDown)
            {
                ToggleActive();
            }

            wasDown = isDown;
        }

        private void MovementTimer_Tick(object? sender, EventArgs e)
        {

            // 🔽 First: global toggle hotkey (from hidden profile)
            if (_globalProfile != null)
            {
                CheckToggleKey(_globalProfile.Key1, ref _toggleKeyWasDown);
            }

            // Handle setup keybinds globally
            if (_currentProfile != null)
            {
                CheckKeybind(_currentProfile.Key1, ref _key1WasDown, 1);
                CheckKeybind(_currentProfile.Key2, ref _key2WasDown, 2);

                // NEW: Setup 3 only for Maestro
                if (string.Equals(_currentProfile.Name, "Maestro", StringComparison.OrdinalIgnoreCase))
                {
                    CheckKeybind(_currentProfile.Key3, ref _key3WasDown, 3);
                }
            }


            // GLOBAL mouse state (works even when game is fullscreen)
            bool rightDown = IsKeyPressed(Keys.RButton);
            bool leftDown = IsKeyPressed(Keys.LButton);

            if (_isActive)
            {
                if (rightDown && !leftDown)
                {
                    // Right button held, waiting for left button to engage combo
                    _comboArmed = true;
                    _comboActive = false;
                }
                else if (rightDown && leftDown && _comboArmed)
                {
                    // Both buttons pressed after arming -> start movement
                    _comboActive = true;
                }
                else if (!rightDown)
                {
                    // Right button released -> fully reset combo
                    _comboArmed = false;
                    _comboActive = false;
                }
                else if (!leftDown)
                {
                    // Left button released but right still down -> keep armed but stop movement
                    _comboActive = false;
                }

                if (_comboActive)
                {
                    ApplyMouseMovement();
                }
            }
            else
            {
                _comboArmed = false;
                _comboActive = false;
            }
        }

        private bool IsKeyPressed(Keys key)
        {
            if (key == Keys.None)
                return false;

            short state = GetAsyncKeyState(key);
            return (state & 0x8000) != 0;
        }

        private void CheckKeybind(Keys key, ref bool wasDown, int setupIndex)
        {
            bool isDown = IsKeyPressed(key);

            if (isDown && !wasDown && _currentProfile != null)
            {
                ApplyProfileSetup(_currentProfile, setupIndex);
                UpdateSetupSummaryLabels();
            }

            wasDown = isDown;
        }

        private void ApplyMouseMovement()
        {
            _accumulatedX += _horizontalSpeed * HorizontalStrengthMultiplier;
            _accumulatedY += _verticalSpeed * VerticalStrengthMultiplier;


            int dx = (int)_accumulatedX;
            int dy = (int)_accumulatedY;

            _accumulatedX -= dx;
            _accumulatedY -= dy;

            if (dx == 0 && dy == 0)
                return;

            // Send relative mouse movement so games see it as real input
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_MOVE,   // relative move
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            INPUT[] inputs = { input };
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
