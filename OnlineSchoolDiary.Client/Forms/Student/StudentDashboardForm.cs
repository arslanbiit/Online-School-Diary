using System.Text.Json;
using OnlineSchoolDiary.Client.Infrastructure;
using OnlineSchoolDiary.Shared.Models;
using OnlineSchoolDiary.Shared.Protocol;

namespace OnlineSchoolDiary.Client.Forms.Student;

public sealed class StudentDashboardForm : Form
{
    private readonly AppSession _session;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    // Diary
    private readonly DataGridView _gridDiary = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };
    private readonly Button _btnDiaryRefresh = new() { Text = "Refresh" };
    private readonly Button _btnAck = new() { Text = "Mark Completed (Acknowledge)" };
    private HashSet<Guid> _acked = new();

    // Notifications
    private readonly DataGridView _gridNoti = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };
    private readonly Button _btnNotiRefresh = new() { Text = "Refresh" };

    public StudentDashboardForm(AppSession session)
    {
        _session = session;
        Text = $"Student Dashboard - {session.User.FullName}";
        Width = 1100;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(_tabs);

        BuildDiaryTab();
        BuildNotificationsTab();

        Shown += async (_, __) =>
        {
            await RefreshDiaryAsync();
            await RefreshNotificationsAsync();
        };
    }

    private void BuildDiaryTab()
    {
        var page = new TabPage("Diary / Assignments");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8) };
        top.Controls.AddRange(new Control[] { _btnDiaryRefresh, _btnAck });

        page.Controls.Add(_gridDiary);
        page.Controls.Add(top);

        _btnDiaryRefresh.Click += async (_, __) => await RefreshDiaryAsync();
        _btnAck.Click += async (_, __) => await AckSelectedAsync();

        _tabs.TabPages.Add(page);
    }

    private void BuildNotificationsTab()
    {
        var page = new TabPage("Notifications");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8) };
        top.Controls.Add(_btnNotiRefresh);
        page.Controls.Add(_gridNoti);
        page.Controls.Add(top);
        _btnNotiRefresh.Click += async (_, __) => await RefreshNotificationsAsync();
        _tabs.TabPages.Add(page);
    }

    private async Task RefreshDiaryAsync()
    {
        var payload = await _session.Rpc.SendAsync<JsonElement>("student.diary.list", null);
        var entries = payload.GetProperty("entries").Deserialize<List<DiaryEntry>>(JsonDefaults.Options) ?? new();
        _acked = (payload.GetProperty("ackedDiaryEntryIds").Deserialize<List<Guid>>(JsonDefaults.Options) ?? new()).ToHashSet();

        var rows = entries.Select(d => new
        {
            d.Id,
            Class = _session.ClassName(d.ClassId),
            Subject = _session.SubjectName(d.SubjectId),
            d.Date,
            d.Title,
            Completed = _acked.Contains(d.Id),
            d.Text
        }).ToList();

        _gridDiary.DataSource = rows;
    }

    private async Task AckSelectedAsync()
    {
        var idProp = _gridDiary.CurrentRow?.Cells["Id"]?.Value;
        if (idProp is not Guid id) return;
        if (_acked.Contains(id))
        {
            MessageBox.Show(this, "Already acknowledged.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await _session.Rpc.SendAsync<object?>("student.diary.ack", new AckDiaryRequest(id));
        await RefreshDiaryAsync();
    }

    private async Task RefreshNotificationsAsync()
    {
        var list = await _session.Rpc.SendAsync<List<Notification>>("notifications.list", null);
        var rows = list.Select(n => new
        {
            n.SentAt,
            n.Title,
            n.Message,
            n.Audience
        }).ToList();
        _gridNoti.DataSource = rows;
    }
}

