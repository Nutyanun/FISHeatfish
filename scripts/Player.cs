using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
	[Export] public float MaxSpeed = 220f;
	[Export] public float Accel = 900f;
	[Export] public float Friction = 900f;
	[Export] public float BiteCooldown = 0.35f;

	[Export] public string SwimAnimation = "swim";
	[Export] public string BiteAnimation = "bite";

	[Export] public NodePath AnimatedSpritePath { get; set; } = "AnimatedSprite2D";
	[Export] public NodePath MouthAreaPath     { get; set; } = "MouthArea";
	[Export] public NodePath HurtAreaPath      { get; set; } = "HurtArea";

	[Export] public bool  ClampToViewport = false;
	[Export] public float ClampMargin     = 8f;

	private AnimatedSprite2D _anim;
	private Area2D _mouthArea;
	private Area2D _hurtArea;
	private float _biteTimer;

	private readonly HashSet<Fish> _targetsInMouth = new();

	// ===== Helpers =====
	private ScoreManager GetSM()
	{
		return
			GetNodeOrNull<ScoreManager>("%ScoreManager") ??
			GetNodeOrNull<ScoreManager>("/root/Main/ScoreManager") ??
			GetNodeOrNull<ScoreManager>("/root/ScoreManager");
	}

	private Fish ResolveFish(Node n)
	{
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

	// ====== NEW: รองรับ Coin ======
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
		// ให้เหรียญจัดการคะแนนโบนัสเองผ่าน Coin.Eaten (ไม่บวกจาก Player เพื่อไม่ชนกลไกโบนัส)
		coin.Consume();
	}

	public override void _Ready()
	{
		_anim      = GetNodeOrNull<AnimatedSprite2D>(AnimatedSpritePath);
		_mouthArea = GetNodeOrNull<Area2D>(MouthAreaPath);
		_hurtArea  = GetNodeOrNull<Area2D>(HurtAreaPath);

		if (_anim != null && _anim.SpriteFrames?.HasAnimation(SwimAnimation) == true)
			_anim.Play(SwimAnimation);

		if (_mouthArea != null)
		{
			_mouthArea.Monitoring  = true;
			_mouthArea.Monitorable = true;

			// เข้า/ออก: ปลา
			_mouthArea.BodyEntered += body => { if (TryGetFish(body, out var fish)) _targetsInMouth.Add(fish); };
			_mouthArea.BodyExited  += body => { if (TryGetFish(body, out var fish)) _targetsInMouth.Remove(fish); };
			_mouthArea.AreaEntered += area => { if (TryGetFish(area, out var fish)) _targetsInMouth.Add(fish); };
			_mouthArea.AreaExited  += area => { if (TryGetFish(area, out var fish)) _targetsInMouth.Remove(fish); };

			// ====== NEW: เหรียญ — กินทันทีเมื่อแตะปาก ======
			_mouthArea.BodyEntered += body => { if (TryGetCoin(body, out var coin)) EatCoin(coin); };
			_mouthArea.AreaEntered += area => { if (TryGetCoin(area, out var coin)) EatCoin(coin); };
		}

		if (_hurtArea != null)
		{
			_hurtArea.Monitoring  = true;
			_hurtArea.Monitorable = true;

			_hurtArea.BodyEntered += body => { if (TryGetFish(body, out var fish)) CheckDeathOnTouch(fish); };
			_hurtArea.AreaEntered += area => { if (TryGetFish(area, out var fish)) CheckDeathOnTouch(fish); };
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		var sm = GetSM();
		if (sm != null && (sm.IsLevelCleared || sm.IsGameOver)) return;

		Vector2 input = GetMoveInput();
		Vector2 targetVel = input * MaxSpeed;

		Velocity = (input != Vector2.Zero)
			? Velocity.MoveToward(targetVel, Accel * (float)delta)
			: Velocity.MoveToward(Vector2.Zero, Friction * (float)delta);

		MoveAndSlide();

		if (_anim != null && MathF.Abs(Velocity.X) > 1f)
			_anim.FlipH = Velocity.X < 0f;

		if (ClampToViewport) ClampInsideViewport();

		if (_biteTimer > 0f) _biteTimer -= (float)delta;
		if (Input.IsActionJustPressed("bite") || Input.IsActionJustPressed("ui_accept"))
			TryBite();

		// ปรับขนาดตัวปลา
		UpdateSizeBasedOnScore(sm);
	}

	private Vector2 GetMoveInput()
	{
		float x = Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left");
		float y = Input.GetActionStrength("ui_down")  - Input.GetActionStrength("ui_up");
		var v = new Vector2(x, y);
		return v == Vector2.Zero ? v : v.Normalized();
	}

	private void TryBite()
	{
		var sm = GetSM();
		if (sm != null && (sm.IsLevelCleared || sm.IsGameOver)) return;

		if (_biteTimer > 0f) return;
		_biteTimer = BiteCooldown;

		if (_anim != null && _anim.SpriteFrames?.HasAnimation(BiteAnimation) == true)
			_anim.Play(BiteAnimation);

		// กัดเฉพาะ "ปลา" ตามเดิม; เหรียญกินอัตโนมัติเมื่อสัมผัสแล้ว (ด้านบน)
		var toCheck = new List<Fish>(_targetsInMouth);
		foreach (var fish in toCheck)
		{
			if (!IsInstanceValid(fish)) { _targetsInMouth.Remove(fish); continue; }

			if (sm != null && sm.Score < fish.RequiredScore)
			{
				GD.Print($"[Player] Too small to bite {fish.Name}: need {fish.RequiredScore}, have {sm.Score} → DIE");
				sm.LoseLife(1);
				return;
			}

			sm?.AddScore(fish.Points);
			GD.Print($"[Player] Eat {fish.Name}, +{fish.Points}");

			fish.QueueFree();
			_targetsInMouth.Remove(fish);
			break;
		}

		if (_anim != null && _anim.SpriteFrames?.HasAnimation(SwimAnimation) == true)
			_anim.Play(SwimAnimation);
	}

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

		// ค่อย ๆ เปลี่ยนขนาดให้ดู smooth
		Scale = Scale.Lerp(Vector2.One * targetScale, 0.05f);
	}
}
