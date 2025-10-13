using Godot;
using System;
using System.Threading.Tasks;

public partial class DownloadGame : Control
{
	private ProgressBar _bar;//ตัวแปร _bar เอาไว้เก็บ ProgressBar
	private Label _status;//ตัวแปร _status เอาไว้เก็บ Label

	//_Ready() ฟังก์ชันที่ Godot จะเรียกอัตโนมัติเมื่อ Node ถูกเพิ่มลงใน Scene Tree
	public override void _Ready()
	{
		//GetNode<ProgressBar>(...) ค้นหา ProgressBar ที่อยู่ใน VBoxContainer/ProgressBar แล้วเก็บลง _bar
		_bar    = GetNode<ProgressBar>("VBoxContainer/ProgressBar");
		// ค้นหา Label ที่อยู่ใน VBoxContainer/Label แล้วเก็บลง _status
		_status = GetNode<Label>("VBoxContainer/Label");

		_bar.MinValue = 0;//เริ่มต้นที่ 0
		_bar.MaxValue = 100;//ค่ามากสุดคือ 100
		_bar.Value = 0;//ตอนเริ่มโหลดให้แสดงว่า 0%

		//เรียกฟังก์ชัน DownloadAsync() แบบ async เพื่อเริ่ม "โหลด"
		//_ = คือ หมายถึงเรียก Task นี้แต่ไม่จำเป็นต้องรอผลลัพธ์
		_ = DownloadAsync();
	}
	//async Task  ฟังก์ชันนี้ทำงานแบบ asynchronous คือทำงานแบบไม่ต้องรอ
	//ทำงานวนลูปอัพเดท ProgressBar ทีละ 1%
	private async Task DownloadAsync()
	{
		for (int i = 0; i <= 100; i++)
		{
			_bar.Value = i;// อัพเดทค่าของ ProgressBar ให้เท่ากับ i
			_status.Text = $"loading..";

			// หน่วงเวลาเล็กน้อยเพื่อให้ดูเหมือนโหลดจริง
			//await รอ Timer 0.05 วินาที ระหว่างที่รอ 0.05 วินาที เกมยังสามารถวาดภาพ, ฟัง Input, อัพเดท UI ได้ตามปกติ
			await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
		}

		_status.Text = "complete!";//เมื่อโหลดครบ 100% แสดงข้อความว่า complete!

		// ไปหน้า LOGin
		await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
		GetTree().ChangeSceneToFile("res://SceneLogin/Login.tscn");
	}
}
