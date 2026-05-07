using OnlineSchoolDiary.Client.Infrastructure;
using OnlineSchoolDiary.Shared.Models;
using OnlineSchoolDiary.Shared.Protocol;

namespace OnlineSchoolDiary.Client.Forms.Admin;

public sealed class AdminDashboardForm : Form
{
    private readonly AppSession _session;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    // Users
    private readonly DataGridView _gridUsers = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };
    private readonly Button _btnUsersRefresh = new() { Text = "Refresh" };
    private readonly Button _btnUsersAdd = new() { Text = "Add" };
    private readonly Button _btnUsersEdit = new() { Text = "Edit" };
    private readonly Button _btnUsersDelete = new() { Text = "Delete" };

    // Assignments
    private readonly DataGridView _gridAssignments = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };
    private readonly ComboBox _cmbTeacher = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox _cmbClass = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _cmbSubject = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly Button _btnAssignAdd = new() { Text = "Assign" };
    private readonly Button _btnAssignDelete = new() { Text = "Delete Selected" };
    private readonly Button _btnAssignRefresh = new() { Text = "Refresh" };

    // Notifications
    private readonly TextBox _txtNotiTitle = new() { Width = 260 };
    private readonly TextBox _txtNotiMessage = new() { Multiline = true, Height = 80, Width = 460, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox _cmbAudience = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ComboBox _cmbTargetRole = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ComboBox _cmbTargetClass = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _cmbTargetUser = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Button _btnSendNoti = new() { Text = "Send" };

    // Reports
    private readonly Button _btnReportsRefresh = new() { Text = "Refresh" };
    private readonly Label _lblReports = new() { AutoSize = true };

    public AdminDashboardForm(AppSession session)
    {
        _session = session;
        Text = $"Admin Dashboard - {session.User.FullName}";
        Width = 1100;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(_tabs);

        BuildUsersTab();
        BuildAssignmentsTab();
        BuildNotificationsTab();
        BuildReportsTab();

        Shown += async (_, __) =>
        {
            await RefreshUsersAsync();
            await RefreshAssignmentsAsync();
            await RefreshReportsAsync();
            BindLookups();
        };
    }

    private void BuildUsersTab()
    {
        var page = new TabPage("Manage Users");

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };
        top.Controls.AddRange(new Control[] { _btnUsersRefresh, _btnUsersAdd, _btnUsersEdit, _btnUsersDelete });
        page.Controls.Add(_gridUsers);
        page.Controls.Add(top);

        _btnUsersRefresh.Click += async (_, __) => await RefreshUsersAsync();
        _btnUsersAdd.Click += async (_, __) =>
        {
            using var f = new AdminUserEditForm(_session, null);
            if (f.ShowDialog(this) == DialogResult.OK) await RefreshUsersAsync();
        };
        _btnUsersEdit.Click += async (_, __) =>
        {
            var selected = _gridUsers.CurrentRow?.DataBoundItem as User;
            if (selected is null) return;
            using var f = new AdminUserEditForm(_session, selected);
            if (f.ShowDialog(this) == DialogResult.OK) await RefreshUsersAsync();
        };
        _btnUsersDelete.Click += async (_, __) =>
        {
            var selected = _gridUsers.CurrentRow?.DataBoundItem as User;
            if (selected is null) return;
            if (MessageBox.Show(this, $"Delete user '{selected.Username}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            await _session.Rpc.SendAsync<object?>("admin.users.delete", selected.Id);
            await RefreshUsersAsync();
        };

        _tabs.TabPages.Add(page);
    }

    private void BuildAssignmentsTab()
    {
        var page = new TabPage("Assign Classes & Subjects");

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8) };
        top.Controls.AddRange(new Control[]
        {
            new Label{Text="Teacher", AutoSize=true, Padding=new Padding(0,10,0,0)}, _cmbTeacher,
            new Label{Text="Class", AutoSize=true, Padding=new Padding(0,10,0,0)}, _cmbClass,
            new Label{Text="Subject", AutoSize=true, Padding=new Padding(0,10,0,0)}, _cmbSubject,
            _btnAssignAdd, _btnAssignDelete, _btnAssignRefresh
        });

        page.Controls.Add(_gridAssignments);
        page.Controls.Add(top);

        _btnAssignRefresh.Click += async (_, __) => await RefreshAssignmentsAsync();
        _btnAssignAdd.Click += async (_, __) =>
        {
            if (_cmbTeacher.SelectedItem is not User t) return;
            if (_cmbClass.SelectedItem is not ClassRoom c) return;
            if (_cmbSubject.SelectedItem is not Subject s) return;
            await _session.Rpc.SendAsync<object?>("admin.assignments.add", new AssignTeacherRequest(t.Id, c.Id, s.Id));
            await RefreshAssignmentsAsync();
        };
        _btnAssignDelete.Click += async (_, __) =>
        {
            var selected = _gridAssignments.CurrentRow?.DataBoundItem as TeacherAssignment;
            if (selected is null) return;
            await _session.Rpc.SendAsync<object?>("admin.assignments.delete", selected.Id);
            await RefreshAssignmentsAsync();
        };

        _tabs.TabPages.Add(page);
    }

    private void BuildNotificationsTab()
    {
        var page = new TabPage("Send Notifications");

        _cmbAudience.DataSource = Enum.GetValues(typeof(NotificationAudience));
        _cmbTargetRole.DataSource = Enum.GetValues(typeof(UserRole));

        _cmbTargetClass.DataSource = _session.Classes.ToList();
        _cmbTargetClass.DisplayMember = "Name";
        _cmbTargetUser.DisplayMember = "Username";

        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), FlowDirection = FlowDirection.TopDown, AutoScroll = true, WrapContents = false };
        panel.Controls.AddRange(new Control[]
        {
            new Label{Text="Title", AutoSize=true}, _txtNotiTitle,
            new Label{Text="Message", AutoSize=true}, _txtNotiMessage,
            new Label{Text="Audience", AutoSize=true}, _cmbAudience,
            new Label{Text="Target Role (if Audience=Role)", AutoSize=true}, _cmbTargetRole,
            new Label{Text="Target Class (if Audience=Class)", AutoSize=true}, _cmbTargetClass,
            new Label{Text="Target User (if Audience=User)", AutoSize=true}, _cmbTargetUser,
            _btnSendNoti
        });

        _btnSendNoti.Click += async (_, __) =>
        {
            var audience = (NotificationAudience)_cmbAudience.SelectedItem!;
            var role = audience == NotificationAudience.Role ? (UserRole?)_cmbTargetRole.SelectedItem : null;
            var cls = audience == NotificationAudience.Class ? (_cmbTargetClass.SelectedItem as ClassRoom)?.Id : null;
            var usr = audience == NotificationAudience.User ? (_cmbTargetUser.SelectedItem as User)?.Id : null;

            await _session.Rpc.SendAsync<object?>("admin.notifications.send",
                new SendNotificationRequest(_txtNotiTitle.Text, _txtNotiMessage.Text, audience, role, cls, usr));

            MessageBox.Show(this, "Notification sent.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtNotiTitle.Clear();
            _txtNotiMessage.Clear();
        };

        page.Controls.Add(panel);
        _tabs.TabPages.Add(page);
    }

    private void BuildReportsTab()
    {
        var page = new TabPage("Reports");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };
        top.Controls.Add(_btnReportsRefresh);
        page.Controls.Add(top);

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(_lblReports);
        page.Controls.Add(panel);

        _btnReportsRefresh.Click += async (_, __) => await RefreshReportsAsync();
        _tabs.TabPages.Add(page);
    }

    private void BindLookups()
    {
        var teachers = _gridUsers.DataSource as List<User> ?? new();
        var teacherList = teachers.Where(u => u.Role == UserRole.Teacher).ToList();
        var usersList = teachers.ToList();

        _cmbTeacher.DataSource = teacherList;
        _cmbTeacher.DisplayMember = "FullName";

        _cmbClass.DataSource = _session.Classes.ToList();
        _cmbClass.DisplayMember = "Name";

        _cmbSubject.DataSource = _session.Subjects.ToList();
        _cmbSubject.DisplayMember = "Name";

        _cmbTargetUser.DataSource = usersList;
    }

    private async Task RefreshUsersAsync()
    {
        var users = await _session.Rpc.SendAsync<List<User>>("admin.users.list", null);
        _gridUsers.DataSource = users;
        BindLookups();
    }

    private async Task RefreshAssignmentsAsync()
    {
        var list = await _session.Rpc.SendAsync<List<TeacherAssignment>>("admin.assignments.list", null);
        _gridAssignments.DataSource = list;
    }

    private async Task RefreshReportsAsync()
    {
        var r = await _session.Rpc.SendAsync<ReportsResponse>("admin.reports", null);
        _lblReports.Text =
            $"Total Users: {r.TotalUsers}\r\n" +
            $"Teachers: {r.TotalTeachers}\r\n" +
            $"Students: {r.TotalStudents}\r\n" +
            $"Parents: {r.TotalParents}\r\n\r\n" +
            $"Diary Entries: {r.TotalDiaryEntries}\r\n" +
            $"Diary Acknowledgements: {r.TotalAcknowledgements}\r\n";
    }
}

