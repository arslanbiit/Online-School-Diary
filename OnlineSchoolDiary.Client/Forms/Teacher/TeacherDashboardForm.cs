using System.Text.Json;
using OnlineSchoolDiary.Client.Infrastructure;
using OnlineSchoolDiary.Shared.Models;
using OnlineSchoolDiary.Shared.Protocol;

namespace OnlineSchoolDiary.Client.Forms.Teacher;

public sealed class TeacherDashboardForm : Form
{
    private readonly AppSession _session;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    // Diary
    private readonly DataGridView _gridDiary = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };
    private readonly ComboBox _cmbClass = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _cmbSubject = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly DateTimePicker _dtDate = new() { Width = 160 };
    private readonly TextBox _txtTitle = new() { Width = 260 };
    private readonly TextBox _txtText = new() { Multiline = true, Height = 120, Width = 520, ScrollBars = ScrollBars.Vertical };
    private readonly Button _btnDiaryNew = new() { Text = "New" };
    private readonly Button _btnDiarySave = new() { Text = "Save" };
    private readonly Button _btnDiaryDelete = new() { Text = "Delete" };
    private readonly Button _btnDiaryRefresh = new() { Text = "Refresh" };
    private Guid? _editingDiaryId;

    // Chat
    private readonly ComboBox _cmbParent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly Button _btnChatLoad = new() { Text = "Load Chat" };
    private readonly ListBox _lstChat = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtChat = new() { Dock = DockStyle.Fill };
    private readonly Button _btnChatSend = new() { Text = "Send", Width = 100 };

    // Ack counts
    private readonly DataGridView _gridAcks = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };
    private readonly Button _btnAcksRefresh = new() { Text = "Refresh" };

    private List<User> _parents = new();

    public TeacherDashboardForm(AppSession session)
    {
        _session = session;
        Text = $"Teacher Dashboard - {session.User.FullName}";
        Width = 1200;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(_tabs);

        BuildDiaryTab();
        BuildChatTab();
        BuildAckTab();

        Shown += async (_, __) =>
        {
            BindDiaryLookups();
            await RefreshDiaryAsync();
            await RefreshAckCountsAsync();
            await LoadContactsAsync();
        };
    }

    private void BuildDiaryTab()
    {
        var page = new TabPage("Diary (Create/Edit/Delete)");

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8) };
        top.Controls.AddRange(new Control[] { _btnDiaryNew, _btnDiarySave, _btnDiaryDelete, _btnDiaryRefresh });

        var editor = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 210,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight,
            AutoScroll = true,
            WrapContents = true
        };

        editor.Controls.AddRange(new Control[]
        {
            new Label{Text="Class", AutoSize=true, Padding=new Padding(0,8,0,0)}, _cmbClass,
            new Label{Text="Subject", AutoSize=true, Padding=new Padding(0,8,0,0)}, _cmbSubject,
            new Label{Text="Date", AutoSize=true, Padding=new Padding(0,8,0,0)}, _dtDate,
            new Label{Text="Title", AutoSize=true, Padding=new Padding(0,8,0,0)}, _txtTitle,
            new Label{Text="Text", AutoSize=true, Padding=new Padding(0,8,0,0)}, _txtText
        });

        page.Controls.Add(_gridDiary);
        page.Controls.Add(editor);
        page.Controls.Add(top);

        _btnDiaryRefresh.Click += async (_, __) => await RefreshDiaryAsync();
        _btnDiaryNew.Click += (_, __) => ClearEditor();
        _btnDiarySave.Click += async (_, __) => await SaveDiaryAsync();
        _btnDiaryDelete.Click += async (_, __) => await DeleteSelectedDiaryAsync();
        _gridDiary.SelectionChanged += (_, __) => LoadSelectedDiaryIntoEditor();

        _tabs.TabPages.Add(page);
    }

    private void BuildChatTab()
    {
        var page = new TabPage("Chat with Parents");

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8) };
        top.Controls.AddRange(new Control[]
        {
            new Label{Text="Parent", AutoSize=true, Padding=new Padding(0,8,0,0)}, _cmbParent, _btnChatLoad
        });

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(8) };
        var sendPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        sendPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sendPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        sendPanel.Controls.Add(_txtChat, 0, 0);
        sendPanel.Controls.Add(_btnChatSend, 1, 0);
        bottom.Controls.Add(sendPanel);

        page.Controls.Add(_lstChat);
        page.Controls.Add(bottom);
        page.Controls.Add(top);

        _btnChatLoad.Click += async (_, __) => await LoadChatAsync();
        _btnChatSend.Click += async (_, __) => await SendChatAsync();

        _tabs.TabPages.Add(page);
    }

    private void BuildAckTab()
    {
        var page = new TabPage("Completion Counts");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8) };
        top.Controls.Add(_btnAcksRefresh);
        page.Controls.Add(_gridAcks);
        page.Controls.Add(top);
        _btnAcksRefresh.Click += async (_, __) => await RefreshAckCountsAsync();
        _tabs.TabPages.Add(page);
    }

    private void BindDiaryLookups()
    {
        var myAssignments = _session.Assignments.Where(a => a.TeacherId == _session.User.Id).ToList();
        var classIds = myAssignments.Select(a => a.ClassId).Distinct().ToHashSet();
        var subjectIds = myAssignments.Select(a => a.SubjectId).Distinct().ToHashSet();

        _cmbClass.DataSource = _session.Classes.Where(c => classIds.Contains(c.Id)).ToList();
        _cmbClass.DisplayMember = "Name";

        _cmbSubject.DataSource = _session.Subjects.Where(s => subjectIds.Contains(s.Id)).ToList();
        _cmbSubject.DisplayMember = "Name";
    }

    private async Task RefreshDiaryAsync()
    {
        var list = await _session.Rpc.SendAsync<List<DiaryEntry>>("teacher.diary.list", null);
        _gridDiary.DataSource = list;
    }

    private void LoadSelectedDiaryIntoEditor()
    {
        if (_gridDiary.CurrentRow?.DataBoundItem is not DiaryEntry d) return;
        _editingDiaryId = d.Id;

        _txtTitle.Text = d.Title;
        _txtText.Text = d.Text;
        _dtDate.Value = d.Date.ToDateTime(TimeOnly.FromDateTime(DateTime.Now));

        var cls = _session.Classes.FirstOrDefault(c => c.Id == d.ClassId);
        var sub = _session.Subjects.FirstOrDefault(s => s.Id == d.SubjectId);
        if (cls is not null) _cmbClass.SelectedItem = cls;
        if (sub is not null) _cmbSubject.SelectedItem = sub;
    }

    private void ClearEditor()
    {
        _editingDiaryId = null;
        _txtTitle.Clear();
        _txtText.Clear();
        _dtDate.Value = DateTime.Today;
    }

    private async Task SaveDiaryAsync()
    {
        if (_cmbClass.SelectedItem is not ClassRoom cls) return;
        if (_cmbSubject.SelectedItem is not Subject sub) return;

        var req = new UpsertDiaryRequest(
            _editingDiaryId,
            cls.Id,
            sub.Id,
            DateOnly.FromDateTime(_dtDate.Value.Date),
            _txtTitle.Text,
            _txtText.Text
        );

        await _session.Rpc.SendAsync<DiaryEntry>("teacher.diary.upsert", req);
        await RefreshDiaryAsync();
        ClearEditor();
    }

    private async Task DeleteSelectedDiaryAsync()
    {
        if (_gridDiary.CurrentRow?.DataBoundItem is not DiaryEntry d) return;
        if (MessageBox.Show(this, "Delete selected diary?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        await _session.Rpc.SendAsync<object?>("teacher.diary.delete", d.Id);
        await RefreshDiaryAsync();
        await RefreshAckCountsAsync();
        ClearEditor();
    }

    private async Task RefreshAckCountsAsync()
    {
        var list = await _session.Rpc.SendAsync<List<JsonElement>>("teacher.diary.ackCounts", null);
        var rows = list.Select(e => new
        {
            DiaryEntryId = e.GetProperty("diaryEntryId").GetGuid(),
            Class = _session.ClassName(e.GetProperty("classId").GetString()),
            Subject = _session.SubjectName(e.GetProperty("subjectId").GetString()!),
            Date = e.GetProperty("date").GetString(),
            TotalStudents = e.GetProperty("totalStudents").GetInt32(),
            Completed = e.GetProperty("ackCount").GetInt32()
        }).ToList();
        _gridAcks.DataSource = rows;
    }

    private async Task LoadContactsAsync()
    {
        var contacts = await _session.Rpc.SendAsync<JsonElement>("meta.contacts", null);
        _parents = contacts.GetProperty("parents").Deserialize<List<User>>(JsonDefaults.Options) ?? new();
        _cmbParent.DataSource = _parents;
        _cmbParent.DisplayMember = "FullName";
    }

    private async Task LoadChatAsync()
    {
        if (_cmbParent.SelectedItem is not User parent) return;
        var msgs = await _session.Rpc.SendAsync<List<ChatMessage>>("chat.get", new GetChatRequest(_session.User.Id, parent.Id));
        _lstChat.DataSource = msgs.Select(m => $"{m.SentAt.LocalDateTime:t} {(m.SenderId == _session.User.Id ? "Me" : parent.FullName)}: {m.Text}").ToList();
    }

    private async Task SendChatAsync()
    {
        if (_cmbParent.SelectedItem is not User parent) return;
        var text = _txtChat.Text.Trim();
        if (text.Length == 0) return;

        await _session.Rpc.SendAsync<ChatMessage>("chat.send", new SendChatMessageRequest(_session.User.Id, parent.Id, text));
        _txtChat.Clear();
        await LoadChatAsync();
    }
}

