using Godot;
using System;

public partial class Fish : CharacterBody2D
{
	[Export] public Vector2 Direction = Vector2.Right;
	[Export] public float Speed = 120f;
	[Export] public float WaveAmplitude = 16f;
	[Export] public float WaveFrequency = 1.0f;
	[Export] public float DespawnMargin = 220f;

	[Export] public string FishType = "fish1";  // ✅ ชนิดของปลา
	[Export] public int Points = 1;             // แต้มเมื่อกินได้
	[Export] public int RequiredScore = 0;      // คะแนนขั้นต่ำที่ผู้เล่นต้องมี ถึงจะกินตัวนี้ได้

	private AnimatedSprite2D _anim;
	private float _t;

	public override void _Ready()
	{
		_anim = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		AddToGroup("fish");
		if (_anim != null) _anim.FlipH = Direction.X < 0;

		// ✅ ตรวจชื่อซีนแบบครอบจักรวาล
		var scene = GetSceneFilePath().ToLower().Replace(" ", "").Replace("_", "");

		if (scene.Contains("fish1")) FishType = "fish1";
		else if (scene.Contains("fish2")) FishType = "fish2";
		else if (scene.Contains("fish3")) FishType = "fish3";
		else if (scene.Contains("shark")) FishType = "shark";
		else FishType = "fish1"; // fallback ปลอดภัยสุด

		//GD.Print($"[Fish] Loaded from {scene} → type={FishType}");
	}

	public override void _PhysicsProcess(double delta)
	{
		_t += (float)delta;
		var dir = Direction.Normalized();
		var wave = Mathf.Sin(_t * Mathf.Tau * WaveFrequency) * WaveAmplitude;

		Velocity = new Vector2(dir.X * Speed, wave);
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
		GD.Print($"[Fish] {FishType} eaten → total now = " +
			(GameProgress.FishCountByType.ContainsKey(FishType)
				? GameProgress.FishCountByType[FishType]
				: 0));
		QueueFree();
	}
}
