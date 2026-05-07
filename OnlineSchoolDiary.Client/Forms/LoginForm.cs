using System.Text.Json;
using OnlineSchoolDiary.Client.Infrastructure;
using OnlineSchoolDiary.Shared.Models;
using OnlineSchoolDiary.Shared.Protocol;

namespace OnlineSchoolDiary.Client.Forms;

public sealed class LoginForm : Form
{
    private readonly TextBox _txtHost = new() { Text = "127.0.0.1" };
    private readonly NumericUpDown _numPort = new() { Minimum = 1, Maximum = 65535, Value = 5050 };
    private readonly TextBox _txtUsername = new() { Text = "admin" };
    private readonly TextBox _txtPassword = new() { Text = "admin", UseSystemPasswordChar = true };
    private readonly Button _btnLogin = new() { Text = "Login", Width = 120 };
    private readonly Label _lblStatus = new() { AutoSize = true };

    public LoginForm()
    {
        Text = "Online School Diary - Login";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 520;
        Height = 320;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(16),
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

        layout.Controls.Add(new Label { Text = "Server Host", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_txtHost, 1, 0);

        layout.Controls.Add(new Label { Text = "Server Port", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_numPort, 1, 1);

        layout.Controls.Add(new Label { Text = "Username", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_txtUsername, 1, 2);

        layout.Controls.Add(new Label { Text = "Password", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        layout.Controls.Add(_txtPassword, 1, 3);

        var panelButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        panelButtons.Controls.Add(_btnLogin);
        layout.Controls.Add(panelButtons, 1, 4);

        layout.Controls.Add(_lblStatus, 1, 5);

        Controls.Add(layout);

        AcceptButton = _btnLogin;
        _btnLogin.Click += async (_, __) => await DoLoginAsync();
    }

    private async Task DoLoginAsync()
    {
        try
        {
            SetUiBusy(true, "Connecting...");

            var rpc = new RpcClient();
            await rpc.ConnectAsync(_txtHost.Text.Trim(), (int)_numPort.Value);

            SetUiBusy(true, "Logging in...");
            var loginResp = await rpc.SendAsync<JsonElement>("auth.login",
                new LoginRequest(_txtUsername.Text.Trim(), _txtPassword.Text));

            var token = loginResp.GetProperty("token").GetString() ?? throw new InvalidOperationException("No token.");
            var user = loginResp.GetProperty("user").Deserialize<User>(JsonDefaults.Options) ?? throw new InvalidOperationException("No user.");
            await rpc.SetSessionTokenOnServerAsync(token);

            SetUiBusy(true, "Loading...");
            var bootstrap = await rpc.SendAsync<JsonElement>("meta.bootstrap", null);
            var session = new AppSession { Rpc = rpc, Token = token, User = user };
            session.Classes.AddRange(bootstrap.GetProperty("classes").Deserialize<List<ClassRoom>>(JsonDefaults.Options) ?? new());
            session.Subjects.AddRange(bootstrap.GetProperty("subjects").Deserialize<List<Subject>>(JsonDefaults.Options) ?? new());
            session.Assignments.AddRange(bootstrap.GetProperty("assignments").Deserialize<List<TeacherAssignment>>(JsonDefaults.Options) ?? new());

            Hide();
            Form dashboard = user.Role switch
            {
                UserRole.Admin => new Admin.AdminDashboardForm(session),
                UserRole.Teacher => new Teacher.TeacherDashboardForm(session),
                UserRole.Student => new Student.StudentDashboardForm(session),
                UserRole.Parent => new Parent.ParentDashboardForm(session),
                _ => throw new InvalidOperationException("Unknown role.")
            };
            dashboard.FormClosed += async (_, __) =>
            {
                await session.Rpc.DisposeAsync();
                Close();
            };
            dashboard.Show();
        }
        catch (Exception ex)
        {
            SetUiBusy(false, ex.Message);
        }
    }

    private void SetUiBusy(bool busy, string status)
    {
        _btnLogin.Enabled = !busy;
        _txtHost.Enabled = !busy;
        _numPort.Enabled = !busy;
        _txtUsername.Enabled = !busy;
        _txtPassword.Enabled = !busy;
        _lblStatus.Text = status;
    }
}

