using Godot;
using System;

public partial class Level : Sprite2D
{
	private float _time = 0f;
	private bool _isActive = false;
	private bool _isDone = false;

	public void SetActive(bool active)
	{
		_isActive = active;
		SetProcess(true); // ให้ _Process ทำงานเสมอ
	}

	public void SetDone(bool done)
	{
		_isDone = done;
	}

	public override void _Process(double delta)
	{
		if (_isActive)
		{
			// 🌟 โค้ดวิบวับ
			_time += (float)delta * 5f;
			float brightness = (Mathf.Sin(_time) + 1f) / 4f + 0.5f;
			SelfModulate = new Color(brightness, brightness, brightness, 1f);
		}
		else if (_isDone)
		{
			// ด่านที่ผ่านแล้ว = สีแดง
			SelfModulate = new Color(1f, 0.4f, 0.4f, 1f);
		}
		else
		{
			// ด่านล็อก = ซีด
			SelfModulate = new Color(1f, 1f, 1f, 1f);
		}
	}

	public override void _Input(InputEvent @event)
{
	if (!_isActive && !_isDone) return; // ยังไม่ถึง → ห้ามกด

	if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
	{
		Vector2 mousePos = mb.Position;
		if (IsPointOverSprite(mousePos))
		{
			// ถ้าเลเวลนี้คือ Active ปัจจุบัน → เล่นจบแล้ว Advance
			if (_isActive)
			{
				GameProgress.CurrentPlayingLevel = LevelNumber;  
				GetTree().ChangeSceneToFile($"res://sceneexplaingame/explaingame{LevelNumber}.tscn");
				// จบเลเวลนี้แล้วค่อยเรียก Advance() ในตอน Win Scene ไม่ใช่ตอนกดเข้า
			}
			// ถ้าเป็น Done (ผ่านแล้ว) → เล่นซ้ำได้ แต่ไม่ Advance
			else if (_isDone)
			{
				GameProgress.CurrentPlayingLevel = LevelNumber;  
				GetTree().ChangeSceneToFile($"res://sceneexplaingame/explaingame{LevelNumber}.tscn");
			}
		}
	}
}

	private bool IsPointOverSprite(Vector2 globalPoint)
	{
		var tex = Texture;
		if (tex == null) return false;

		Vector2 texSize = tex.GetSize();
		Vector2 scaledSize = texSize * Scale;
		Vector2 topLeft = GlobalPosition - scaledSize * 0.5f;
		Rect2 rect = new Rect2(topLeft, scaledSize);

		return rect.HasPoint(globalPoint);
	}

	[Export] public int LevelNumber { get; set; } = 1;
}
