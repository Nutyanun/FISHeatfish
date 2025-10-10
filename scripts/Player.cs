using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
	// === CONFIG พื้นฐาน ===
	[Export] public float MaxSpeed = 220f;
	[Export] public float Accel = 900f;
	[Export] public float Friction = 900f;
	[Export] public float BiteCooldown = 0.35f;

	// === ANIMATION NAMES ===
	[Export] public string SwimAnimation = "swim";
	[Export] public string BiteAnimation = "bite";

	// === NODE PATHS ===
	[Export] public NodePath AnimatedSpritePath { get; set; } = "AnimatedSprite2D";
	[Export] public NodePath MouthAreaPath     { get; set; } = "MouthArea";
	[Export] public NodePath HurtAreaPath      { get; set; } = "HurtArea";

	// === การจำกัดขอบจอ ===
	[Export] public bool  ClampToViewport = false;
	[Export] public float ClampMargin     = 8f;

	// === ตัวแปรภายใน ===
	private AnimatedSprite2D _anim;
	private Area2D _mouthArea;
	private Area2D _hurtArea;
	private float _biteTimer;

	// === set เก็บเป้าหมายที่อยู่ในปาก ===
	private readonly HashSet<Fish> _targetsInMouth = new();

	// ===========================================
	// 🟦 ส่วน Helper: หา Node อื่น และ Resolve Object
	// ===========================================

	private ScoreManager GetSM()
	{
		// หา ScoreManager ได้ทั้งแบบ local และ global
		return
			GetNodeOrNull<ScoreManager>("%ScoreManager") ??
			GetNodeOrNull<ScoreManager>("/root/Main/ScoreManager") ??
			GetNodeOrNull<ScoreManager>("/root/ScoreManager");
	}

	private Fish ResolveFish(Node n)
	{
		// หาว่า node ที่ชนคือ Fish หรือเป็นลูกของ Fish
		if (n is Fish f) return f;
		if (n.GetParent() is Fish pf) return pf;
		if (n.GetOwner() is Fish of) return of;
		return n.GetParent()?.GetParent() as Fish;
	}

	private bool TryGetFish(Node n, out Fish fish)
	{
		fish = ResolveFish(n);
		return fish != null;
	}

	// ====== รองรับ Coin ======
	private Coin ResolveCoin(Node n)
	{
		if (n is Coin c) return c;
		if (n.GetParent() is Coin pc) return pc;
		if (n.GetOwner() is Coin oc) return oc;
		return n.GetParent()?.GetParent() as Coin;
	}

	private bool TryGetCoin(Node n, out Coin coin)
	{
		coin = ResolveCoin(n);
		return coin != null;
	}

	private void EatCoin(Coin coin)
	{
		if (coin == null || !IsInstanceValid(coin)) return;
		// ให้ Coin จัดการโบนัสเอง (ไม่บวกผ่าน Player เพื่อไม่ชนกลไก bonus)
		coin.Consume();
	}

	// ===========================================
	// 🟩 _Ready(): Setup ทุกอย่าง
	// ===========================================
	public override void _Ready()
	{
		_anim      = GetNodeOrNull<AnimatedSprite2D>(AnimatedSpritePath);
		_mouthArea = GetNodeOrNull<Area2D>(MouthAreaPath);
		_hurtArea  = GetNodeOrNull<Area2D>(HurtAreaPath);

		if (_anim != null && _anim.SpriteFrames?.HasAnimation(SwimAnimation) == true)
			_anim.Play(SwimAnimation);

		// --- Mouth Area ---
		if (_mouthArea != null)
		{
			_mouthArea.Monitoring  = true;
			_mouthArea.Monitorable = true;

			// 🐟 ตรวจการชนของปลา
			_mouthArea.BodyEntered += body => { if (TryGetFish(body, out var fish)) _targetsInMouth.Add(fish); };
			_mouthArea.BodyExited  += body => { if (TryGetFish(body, out var fish)) _targetsInMouth.Remove(fish); };
			_mouthArea.AreaEntered += area => { if (TryGetFish(area, out var fish)) _targetsInMouth.Add(fish); };
			_mouthArea.AreaExited  += area => { if (TryGetFish(area, out var fish)) _targetsInMouth.Remove(fish); };

			// 💰 Coin: กินทันทีเมื่อโดนปาก
			_mouthArea.BodyEntered += body => { if (TryGetCoin(body, out var coin)) EatCoin(coin); };
			_mouthArea.AreaEntered += area => { if (TryGetCoin(area, out var coin)) EatCoin(coin); };
		}

		// --- Hurt Area ---
		if (_hurtArea != null)
		{
			_hurtArea.Monitoring  = true;
			_hurtArea.Monitorable = true;

			// ชนปลาที่ใหญ่เกิน → ตาย
			_hurtArea.BodyEntered += body => { if (TryGetFish(body, out var fish)) CheckDeathOnTouch(fish); };
			_hurtArea.AreaEntered += area => { if (TryGetFish(area, out var fish)) CheckDeathOnTouch(fish); };
		}
	}

	// ===========================================
	// 🟨 _PhysicsProcess(): อัปเดตการเคลื่อนไหว + กัด
	// ===========================================
	public override void _PhysicsProcess(double delta)
	{
		var sm = GetSM();
		if (sm != null && (sm.IsLevelCleared || sm.IsGameOver)) return;

		// --- เคลื่อนที่ ---
		Vector2 input = GetMoveInput();
		Vector2 targetVel = input * MaxSpeed;

		Velocity = (input != Vector2.Zero)
			? Velocity.MoveToward(targetVel, Accel * (float)delta)
			: Velocity.MoveToward(Vector2.Zero, Friction * (float)delta);

		MoveAndSlide();

		// --- พลิก sprite ตามทิศทาง ---
		if (_anim != null && MathF.Abs(Velocity.X) > 1f)
			_anim.FlipH = Velocity.X < 0f;

		// --- จำกัดไม่ให้ออกนอกจอ ---
		if (ClampToViewport) ClampInsideViewport();

		// --- จัดการ cooldown การกัด ---
		if (_biteTimer > 0f) _biteTimer -= (float)delta;
		if (Input.IsActionJustPressed("bite") || Input.IsActionJustPressed("ui_accept"))
			TryBite();

		// --- ปรับขนาดตัวปลา ---
		UpdateSizeBasedOnScore(sm);
	}

	private Vector2 GetMoveInput()
	{
		float x = Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left");
		float y = Input.GetActionStrength("ui_down")  - Input.GetActionStrength("ui_up");
		var v = new Vector2(x, y);
		return v == Vector2.Zero ? v : v.Normalized();
	}

	// ===========================================
	// 🟥 TryBite(): การกัดปลา
	// ===========================================
	private void TryBite()
	{
		var sm = GetSM();
		if (sm != null && (sm.IsLevelCleared || sm.IsGameOver)) return;

		if (_biteTimer > 0f) return;
		_biteTimer = BiteCooldown;

		// --- เล่น animation กัด ---
		if (_anim != null && _anim.SpriteFrames?.HasAnimation(BiteAnimation) == true)
			_anim.Play(BiteAnimation);

		// --- เช็กปลาที่อยู่ในปากทั้งหมด ---
		var toCheck = new List<Fish>(_targetsInMouth);
		foreach (var fish in toCheck)
		{
			if (!IsInstanceValid(fish)) { _targetsInMouth.Remove(fish); continue; }

			// ถ้าเล็กเกินไป → ตาย
			if (sm != null && sm.Score < fish.RequiredScore)
			{
				GD.Print($"[Player] Too small to bite {fish.Name}: need {fish.RequiredScore}, have {sm.Score} → DIE");
				sm.LoseLife(1);
				return;
			}

			// ✅ บวกคะแนน (ส่ง fishType ไปด้วย)
			sm?.AddScore(fish.Points, fish.FishType);

			// ✅ เรียกให้ปลาอัปเดต GameProgress (นับจำนวนปลา)
			fish.OnEaten();

			GD.Print($"[Player] Eat {fish.FishType}, +{fish.Points}");

			_targetsInMouth.Remove(fish);
			break;
		}

		// --- กลับไปแอนิเมชันว่าย ---
		if (_anim != null && _anim.SpriteFrames?.HasAnimation(SwimAnimation) == true)
			_anim.Play(SwimAnimation);
	}

	// ===========================================
	// ☠️ CheckDeathOnTouch(): ชนปลาที่ใหญ่เกิน
	// ===========================================
	private void CheckDeathOnTouch(Fish fish)
	{
		var sm = GetSM();
		if (sm == null || sm.IsLevelCleared || sm.IsGameOver) return;
		if (!IsInstanceValid(fish)) return;

		if (sm.Score < fish.RequiredScore)
		{
			GD.Print($"[Player] Touch big {fish.Name}: need {fish.RequiredScore}, have {sm.Score} → DIE");
			sm.LoseLife(1);
		}
	}

	// ===========================================
	// 🟩 ClampInsideViewport(): ป้องกันออกนอกจอ
	// ===========================================
	private void ClampInsideViewport()
	{
		var rect = GetViewportRect();
		float minX = rect.Position.X + ClampMargin;
		float maxX = rect.End.X     - ClampMargin;
		float minY = rect.Position.Y + ClampMargin;
		float maxY = rect.End.Y     - ClampMargin;

		GlobalPosition = new Vector2(
			Mathf.Clamp(GlobalPosition.X, minX, maxX),
			Mathf.Clamp(GlobalPosition.Y, minY, maxY)
		);
	}

	private void UpdateSizeBasedOnScore(ScoreManager sm)
	{
		if (sm == null) return;

		float targetScale = 1.0f; // ขนาดเริ่มต้น

		if (sm.Score >= 30)
			targetScale = 2.4f; // ใหญ่สุด
		else if (sm.Score >= 15)
			targetScale = 1.9f;
		else if (sm.Score >= 10)
			targetScale = 1.3f;

		// ค่อย ๆ เปลี่ยนขนาดให้ smooth
		Scale = Scale.Lerp(Vector2.One * targetScale, 0.05f);
	}
}
