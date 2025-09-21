using Godot;
using System;
using System.Threading.Tasks;

public partial class DownloadGame : Control
{
	private ProgressBar _bar;
	private Label _status;

	public override void _Ready()
	{
		_bar    = GetNode<ProgressBar>("VBoxContainer/ProgressBar");
		_status = GetNode<Label>("VBoxContainer/Label");

		_bar.MinValue = 0;
		_bar.MaxValue = 100;
		_bar.Value = 0;

		// เริ่มโหลดหลอก
		_ = FakeDownloadAsync();
	}

	private async Task FakeDownloadAsync()
	{
		for (int i = 0; i <= 100; i++)
		{
			_bar.Value = i;
			_status.Text = $"loading..";

			// หน่วงเวลาเล็กน้อยเพื่อให้ดูเหมือนโหลดจริง
			await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
		}

		_status.Text = "complete!";

		// ไปหน้า LOGin
		await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
		GetTree().ChangeSceneToFile("res://SceneLogin/Login.tscn");
	}
}
