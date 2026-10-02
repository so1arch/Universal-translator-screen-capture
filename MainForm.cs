using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenTranslator;

public partial class MainForm : Form
{
    private readonly Settings settings;
    private readonly Translator translator;
    private readonly System.Windows.Forms.Timer processTimer;
    private ComboBox comboWindows;
    private Button btnRefresh;
    private Label lblStatus;

    public MainForm()
    {
        settings = Settings.Load();
        translator = new Translator();

        InitializeComponent();

        processTimer = new System.Windows.Forms.Timer();
        processTimer.Interval = Math.Max(100, 1000 / Math.Max(1, settings.Fps));
        processTimer.Tick += OnTimerTick;
        processTimer.Start();

        RefreshWindowList();
    }

    private void InitializeComponent()
    {
        Text = "Screen Translator";
        Size = new Size(420, 180);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        comboWindows = new ComboBox
        {
            Location = new Point(12, 12),
            Size = new Size(270, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        btnRefresh = new Button
        {
            Text = "Обновить",
            Location = new Point(295, 10),
            Size = new Size(95, 27)
        };
        btnRefresh.Click += (s, e) => RefreshWindowList();

        lblStatus = new Label
        {
            Text = "Статус: Готово к работе",
            Location = new Point(12, 50),
            AutoSize = true
        };

        Controls.Add(comboWindows);
        Controls.Add(btnRefresh);
        Controls.Add(lblStatus);
    }

    private void RefreshWindowList()
    {
        comboWindows.Items.Clear();
        var windows = Native.Windows();
        foreach (var win in windows)
        {
            comboWindows.Items.Add(win);
        }
        if (comboWindows.Items.Count > 0)
            comboWindows.SelectedIndex = 0;
    }

    private void OnTimerTick(object sender, EventArgs e)
    {
        lblStatus.Text = $"Статус: Работает | Кэш переводов: {translator.Count}";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        settings.Save();
        translator.Save();
        base.OnFormClosing(e);
    }
}