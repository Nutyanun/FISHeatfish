using Godot;   // ใช้คลาสของ Godot เช่น Node, CharacterBody2D, Vector2 ฯลฯ
using System;  // ใช้ระบบพื้นฐานของ .NET (เช่น interface, Math)

// ===== ให้ปลารองรับการสโลว์แบบเรียกจาก C# โดยตรง =====
public interface ISlowable
{
	void SetSpeedScale(float scale);      // กำหนดสเกลความเร็วใหม่ (เช่น 0.3f = ช้าลง 70%)
	void SetSpeedMultiplier(float scale); // ชื่ออีกแบบของ SetSpeedScale เพื่อความเข้าใจง่าย
	void ClearSlow();                     // คืนค่าความเร็วปกติ (scale = 1f)
}

// คลาส Fish ใช้แทน “ปลาธรรมดา” ที่เคลื่อนไหวในเกม และสามารถโดน slow ได้
public partial class Fish : CharacterBody2D, ISlowable
{
	// ===== Config / Export =====
	[Export] public Vector2 Direction = Vector2.Right; // ทิศทางเริ่มต้นของปลา (ไปขวา)
	[Export] public float Speed = 120f;               // ความเร็วพื้นฐานแกน X
	[Export] public float WaveAmplitude = 16f;        // ขนาดการแกว่งขึ้น-ลง
	[Export] public float WaveFrequency = 1.0f;       // ความถี่การแกว่ง (Hz)
	[Export] public float DespawnMargin = 220f;       // ระยะขอบจอที่ถ้าหลุดออกไป → ลบปลา

	[Export] public string FishType = "fish1";        // ชื่อประเภทปลา
	[Export] public int Points = 1;                   // คะแนนเมื่อกินได้
	[Export] public int RequiredScore = 0;            // คะแนนขั้นต่ำก่อนให้ปลาเกิดได้

	// ===== Runtime =====
	private float _speedScale = 1f;                   // ตัวคูณความเร็วปัจจุบัน (1 = ปกติ)
	private AnimatedSprite2D _anim;                   // ตัว sprite ที่แสดงปลาขยับ
	private float _t;                                 // ตัวจับเวลาใช้คำนวณการแกว่ง

	public override void _Ready()
	{
		_anim = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D"); // หา AnimatedSprite2D ลูก
		if (_anim != null) _anim.FlipH = Direction.X < 0;            // กลับภาพถ้าว่ายไปทางซ้าย

		AddToGroup("Fish"); // ใส่ในกลุ่ม “Fish” เพื่อให้ระบบอื่นหาปลาเจอ
		AddToGroup("fish"); // (ซ้ำอีกชื่อเพื่อรองรับโค้ดเก่า/เงื่อนไขอื่น)

		// อ่านชื่อไฟล์ scene แล้วแปลงเป็นชนิดปลาโดยอัตโนมัติ
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
		_t += (float)delta; // เพิ่มเวลาใช้ทำให้ปลาขยับแบบคลื่น

		var dir = Direction.Normalized(); // ทำให้ทิศทางมีความยาว = 1
		var wave = Mathf.Sin(_t * Mathf.Tau * WaveFrequency) * WaveAmplitude; // คำนวณการแกว่งแกน Y

		Velocity = new Vector2(dir.X * Speed * _speedScale, wave); // คำนวณความเร็วรวม (แนวนอน+แกว่ง)
		MoveAndSlide(); // สั่งให้ปลาเคลื่อนตาม Velocity และชนวัตถุอื่นถ้ามี

		var rect = GetViewportRect(); // ดึงขอบจอปัจจุบัน
		if (GlobalPosition.X < rect.Position.X - DespawnMargin || // ถ้าหลุดซ้ายเกินขอบ
			GlobalPosition.X > rect.End.X + DespawnMargin ||      // หรือหลุดขวา
			GlobalPosition.Y < rect.Position.Y - DespawnMargin || // หรือหลุดบน
			GlobalPosition.Y > rect.End.Y + DespawnMargin)        // หรือหลุดล่าง
		{
			QueueFree(); // ลบปลานี้ออกจากเกม
		}
	}

	// สุ่มสกิน (แอนิเมชัน) ให้ปลาตัวนี้ใช้ตอนเกิด
	public void ApplyRandomSkin()
	{
		if (_anim?.SpriteFrames == null) return; // ถ้าไม่มี sprite หรือแอนิเมชัน → ข้าม
		var names = _anim.SpriteFrames.GetAnimationNames(); // ดึงรายชื่อแอนิเมชันทั้งหมด
		if (names.Length == 0) return; // ถ้าไม่มีเลย → ข้าม
		int idx = (int)(GD.Randi() % (uint)names.Length); // สุ่ม index
		_anim.Animation = names[idx]; // ตั้งชื่อแอนิเมชันใหม่
		_anim.Play(); // เล่นแอนิเมชันทันที
	}

	// เรียกเมื่อปลาถูกกิน
	public void OnEaten()
	{
		GameProgress.AddFishCount(FishType); // เพิ่มจำนวนปลาที่ถูกกินใน GameProgress

		int total = 0;
		if (GameProgress.FishCountByType != null && GameProgress.FishCountByType.ContainsKey(FishType))
		{
			total = GameProgress.FishCountByType[FishType]; // อ่านจำนวนรวมของปลาชนิดนี้ที่กินไปแล้ว
		}

		GD.Print($"[Fish] {FishType} eaten → total now = {total}"); // แสดง log ใน console

		// ปิดการชนและลบแบบ deferred เพื่อหลีกเลี่ยง error ตอนยังมี signal วิ่งอยู่
		SetDeferred("monitoring", false); // ปิดระบบตรวจการชน
		SetDeferred("monitorable", false); // ปิดให้ตัวอื่นตรวจชนปลาได้
		CallDeferred("queue_free"); // ลบออกจาก scene หลังเฟรมปัจจุบัน
	}

	// ===== ISlowable =====
	public void SetSpeedScale(float s)
	{
		_speedScale = Mathf.Max(0.05f, s); // ตั้งค่าความเร็วใหม่ แต่ไม่ให้ต่ำกว่า 5% ของปกติ
	}

	public void SetSpeedMultiplier(float s) => SetSpeedScale(s); // ใช้ชื่ออีกแบบ (เหมือน alias)

	public void ClearSlow()
	{
		_speedScale = 1f; // คืนค่าความเร็วปกติ (100%)
	}

	// เปลี่ยนทิศทางของปลา (ใช้ตอน spawn หรือชนขอบ)
	public void SetDirection(Vector2 newDir, bool autoFlip = true)
	{
		Direction = newDir; // อัปเดตทิศทางใหม่
		if (autoFlip && _anim != null) _anim.FlipH = Direction.X < 0; // กลับภาพถ้าว่ายกลับฝั่ง
	}

	// เปลี่ยนความเร็วพื้นฐานของปลา
	public void SetBaseSpeed(float baseSpeed)
	{
		Speed = baseSpeed; // ตั้งค่าความเร็วพื้นฐานใหม่ (ไม่เกี่ยวกับ slow)
	}
}
