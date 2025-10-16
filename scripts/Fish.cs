using Godot;
using System;

// ===== ให้ปลารองรับการสโลว์แบบเรียกจาก C# โดยตรง =====
public interface ISlowable
{
	void SetSpeedScale(float scale);      // กำหนดสเกลความเร็วใหม่ (เช่น 0.3f = ช้าลง)
	void SetSpeedMultiplier(float scale); // alias ของ SetSpeedScale
	void ClearSlow();                     // คืนค่าความเร็วปกติ (1f)
}

public partial class Fish : CharacterBody2D, ISlowable
{
	// ===== Config / Export =====
	[Export] public Vector2 Direction = Vector2.Right;
	[Export] public float Speed = 120f;               // ฐานความเร็วแกน X
	[Export] public float WaveAmplitude = 16f;        // แกน Y แกว่งเป็นคลื่น
	[Export] public float WaveFrequency = 1.0f;
	[Export] public float DespawnMargin = 220f;

	[Export] public string FishType = "fish1";
	[Export] public int Points = 1;
	[Export] public int RequiredScore = 0;

	// ===== Runtime =====
	private float _speedScale = 1f;                   // 1 = ปกติ, <1 = ช้าลง
	private AnimatedSprite2D _anim;
	private float _t;

	public override void _Ready()
	{
		_anim = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (_anim != null) _anim.FlipH = Direction.X < 0;

		AddToGroup("Fish");
		AddToGroup("fish");

		var scene = GetSceneFilePath().ToLower().Replace(" ", "").Replace("_", "");
		if (scene.Contains("fish1")) FishType = "fish1";
		else if (scene.Contains("fish2")) FishType = "fish2";
		else if (scene.Contains("fish3")) FishType = "fish3";
		else if (scene.Contains("sawshark")) FishType = "SawShark";
		else if (scene.Contains("seaangler")) FishType = "Seaangler";
		else if (scene.Contains("shark")) FishType = "shark";
	}

	public override void _PhysicsProcess(double delta)
	{
		_t += (float)delta;

		var dir = Direction.Normalized();
		var wave = Mathf.Sin(_t * Mathf.Tau * WaveFrequency) * WaveAmplitude;

		Velocity = new Vector2(dir.X * Speed * _speedScale, wave);
		MoveAndSlide();

		var rect = GetViewportRect();
		if (GlobalPosition.X < rect.Position.X - DespawnMargin ||
			GlobalPosition.X > rect.End.X + DespawnMargin ||
			GlobalPosition.Y < rect.Position.Y - DespawnMargin ||
			GlobalPosition.Y > rect.End.Y + DespawnMargin)
		{
			QueueFree();
		}
	}

	public void ApplyRandomSkin()
	{
		if (_anim?.SpriteFrames == null) return;
		var names = _anim.SpriteFrames.GetAnimationNames();
		if (names.Length == 0) return;
		int idx = (int)(GD.Randi() % (uint)names.Length);
		_anim.Animation = names[idx];
		_anim.Play();
	}

	public void OnEaten()
	{
		GameProgress.AddFishCount(FishType);

		int total = 0;
		if (GameProgress.FishCountByType != null &&
			GameProgress.FishCountByType.ContainsKey(FishType))
		{
			total = GameProgress.FishCountByType[FishType];
		}

		GD.Print($"[Fish] {FishType} eaten → total now = {total}");

		// ปิดชนและลบแบบ deferred เพื่อกัน flushing_queries ตอนกัด
		SetDeferred("monitoring", false);
		SetDeferred("monitorable", false);
		CallDeferred("queue_free");
	}

	// ===== ISlowable =====
	public void SetSpeedScale(float s)
	{
		_speedScale = Mathf.Max(0.05f, s);
	}

	public void SetSpeedMultiplier(float s) => SetSpeedScale(s);

	public void ClearSlow()
	{
		_speedScale = 1f;
	}

	public void SetDirection(Vector2 newDir, bool autoFlip = true)
	{
		Direction = newDir;
		if (autoFlip && _anim != null) _anim.FlipH = Direction.X < 0;
	}

	public void SetBaseSpeed(float baseSpeed)
	{
		Speed = baseSpeed;
	}
}
