using OnlineSchoolDiary.Client.Infrastructure;
using OnlineSchoolDiary.Shared.Models;
using OnlineSchoolDiary.Shared.Protocol;

namespace OnlineSchoolDiary.Client.Forms.Admin;

public sealed class AdminUserEditForm : Form
{
    private readonly AppSession _session;
    private readonly User? _editUser;

    private readonly TextBox _txtUsername = new() { Width = 240 };
    private readonly TextBox _txtPassword = new() { Width = 240, UseSystemPasswordChar = true };
    private readonly TextBox _txtFullName = new() { Width = 240 };
    private readonly ComboBox _cmbRole = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _cmbClass = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _cmbChildStudent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly Button _btnSave = new() { Text = "Save", Width = 120 };
    private readonly Label _lblInfo = new() { AutoSize = true };

    public AdminUserEditForm(AppSession session, User? editUser)
    {
        _session = session;
        _editUser = editUser;

        Text = editUser is null ? "Add User" : "Edit User";
        StartPosition = FormStartPosition.CenterParent;
        Width = 520;
        Height = 420;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _cmbRole.DataSource = Enum.GetValues(typeof(UserRole));
        _cmbClass.DataSource = session.Classes.ToList();
        _cmbClass.DisplayMember = "Name";

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(14)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

        layout.Controls.Add(new Label { Text = "Username", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_txtUsername, 1, 0);

        layout.Controls.Add(new Label { Text = "Password (only when adding)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_txtPassword, 1, 1);

        layout.Controls.Add(new Label { Text = "Full Name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_txtFullName, 1, 2);

        layout.Controls.Add(new Label { Text = "Role", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        layout.Controls.Add(_cmbRole, 1, 3);

        layout.Controls.Add(new Label { Text = "Class (Students)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        layout.Controls.Add(_cmbClass, 1, 4);

        layout.Controls.Add(new Label { Text = "Child Student (Parents)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        layout.Controls.Add(_cmbChildStudent, 1, 5);

        layout.Controls.Add(_lblInfo, 1, 6);
        layout.Controls.Add(_btnSave, 1, 7);

        Controls.Add(layout);

        _cmbRole.SelectedIndexChanged += async (_, __) => await RefreshChildStudentsAsync();
        _btnSave.Click += async (_, __) => await SaveAsync();

        Load += async (_, __) =>
        {
            await RefreshChildStudentsAsync();
            if (editUser is not null) BindEditUser(editUser);
        };
    }

    private void BindEditUser(User u)
    {
        _txtUsername.Text = u.Username;
        _txtFullName.Text = u.FullName;
        _cmbRole.SelectedItem = u.Role;

        if (u.ClassId is not null)
        {
            var cls = _session.Classes.FirstOrDefault(c => c.Id == u.ClassId);
            if (cls is not null) _cmbClass.SelectedItem = cls;
        }

        if (u.ChildStudentId is not null)
        {
            foreach (var item in _cmbChildStudent.Items)
            {
                if (item is User su && su.Id == u.ChildStudentId)
                {
                    _cmbChildStudent.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private async Task RefreshChildStudentsAsync()
    {
        var role = (UserRole)_cmbRole.SelectedItem!;
        if (role != UserRole.Parent)
        {
            _cmbChildStudent.DataSource = null;
            _cmbChildStudent.Enabled = false;
            _cmbClass.Enabled = role == UserRole.Student;
            _lblInfo.Text = "";
            return;
        }

        _cmbChildStudent.Enabled = true;
        _cmbClass.Enabled = false;
        var users = await _session.Rpc.SendAsync<List<User>>("admin.users.list", null);
        var students = users.Where(x => x.Role == UserRole.Student).ToList();
        _cmbChildStudent.DataSource = students;
        _cmbChildStudent.DisplayMember = "Username";
        _lblInfo.Text = "For Parents: link a student account.";
    }

    private async Task SaveAsync()
    {
        try
        {
            _btnSave.Enabled = false;
            var role = (UserRole)_cmbRole.SelectedItem!;
            var classId = role == UserRole.Student ? (_cmbClass.SelectedItem as ClassRoom)?.Id : null;
            var childStudentId = role == UserRole.Parent ? (_cmbChildStudent.SelectedItem as User)?.Id : null;

            if (_editUser is null)
            {
                if (string.IsNullOrWhiteSpace(_txtPassword.Text))
                    throw new InvalidOperationException("Password is required when creating a user.");

                await _session.Rpc.SendAsync<User>("admin.users.create", new CreateUserRequest(
                    _txtUsername.Text, _txtPassword.Text, role, _txtFullName.Text, classId, childStudentId));
            }
            else
            {
                await _session.Rpc.SendAsync<User>("admin.users.update", new UpdateUserRequest(
                    _editUser.Id, _txtUsername.Text, role, _txtFullName.Text, classId, childStudentId));
            }

            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnSave.Enabled = true;
        }
    }
}

