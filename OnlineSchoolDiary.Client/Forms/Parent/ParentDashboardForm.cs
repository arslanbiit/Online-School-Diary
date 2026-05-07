using System.Text.Json;
using OnlineSchoolDiary.Client.Infrastructure;
using OnlineSchoolDiary.Shared.Models;
using OnlineSchoolDiary.Shared.Protocol;

namespace OnlineSchoolDiary.Client.Forms.Parent;

public sealed class ParentDashboardForm : Form
{
    private readonly AppSession _session;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    // Child diary
    private readonly DataGridView _gridDiary = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };
    private readonly Button _btnDiaryRefresh = new() { Text = "Refresh" };

    // Chat
    private readonly ComboBox _cmbTeacher = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly Button _btnChatLoad = new() { Text = "Load Chat" };
    private readonly ListBox _lstChat = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtChat = new() { Dock = DockStyle.Fill };
    private readonly Button _btnChatSend = new() { Text = "Send", Width = 100 };

    private List<User> _teachers = new();

    public ParentDashboardForm(AppSession session)
    {
        _session = session;
        Text = $"Parent Dashboard - {session.User.FullName}";
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(_tabs);

        BuildChildDiaryTab();
        BuildChatTab();

        Shown += async (_, __) =>
        {
            await RefreshDiaryAsync();
            await LoadContactsAsync();
        };
    }

    private void BuildChildDiaryTab()
    {
        var page = new TabPage("Child Diary");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8) };
        top.Controls.Add(_btnDiaryRefresh);
        page.Controls.Add(_gridDiary);
        page.Controls.Add(top);
        _btnDiaryRefresh.Click += async (_, __) => await RefreshDiaryAsync();
        _tabs.TabPages.Add(page);
    }

    private void BuildChatTab()
    {
        var page = new TabPage("Chat with Teacher");

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8) };
        top.Controls.AddRange(new Control[]
        {
            new Label{Text="Teacher", AutoSize=true, Padding=new Padding(0,8,0,0)}, _cmbTeacher, _btnChatLoad
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

    private async Task RefreshDiaryAsync()
    {
        var list = await _session.Rpc.SendAsync<List<DiaryEntry>>("parent.childDiary.list", null);
        var rows = list.Select(d => new
        {
            Class = _session.ClassName(d.ClassId),
            Subject = _session.SubjectName(d.SubjectId),
            d.Date,
            d.Title,
            d.Text
        }).ToList();
        _gridDiary.DataSource = rows;
    }

    private async Task LoadContactsAsync()
    {
        var contacts = await _session.Rpc.SendAsync<JsonElement>("meta.contacts", null);
        _teachers = contacts.GetProperty("teachers").Deserialize<List<User>>(JsonDefaults.Options) ?? new();
        _cmbTeacher.DataSource = _teachers;
        _cmbTeacher.DisplayMember = "FullName";
    }

    private async Task LoadChatAsync()
    {
        if (_cmbTeacher.SelectedItem is not User teacher) return;
        var msgs = await _session.Rpc.SendAsync<List<ChatMessage>>("chat.get", new GetChatRequest(teacher.Id, _session.User.Id));
        _lstChat.DataSource = msgs.Select(m => $"{m.SentAt.LocalDateTime:t} {(m.SenderId == _session.User.Id ? "Me" : teacher.FullName)}: {m.Text}").ToList();
    }

    private async Task SendChatAsync()
    {
        if (_cmbTeacher.SelectedItem is not User teacher) return;
        var text = _txtChat.Text.Trim();
        if (text.Length == 0) return;

        await _session.Rpc.SendAsync<ChatMessage>("chat.send", new SendChatMessageRequest(teacher.Id, _session.User.Id, text));
        _txtChat.Clear();
        await LoadChatAsync();
    }
}

