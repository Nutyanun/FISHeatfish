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

	// ตั้งชื่อโหนดให้ตรงกับที่อยู่ใน Player.tscn
	[Export] public NodePath AnimatedSpritePath { get; set; } = "AnimatedSprite2D";
	[Export] public NodePath MouthAreaPath     { get; set; } = "MouthArea"; // พื้นที่ "งับ"
	[Export] public NodePath HurtAreaPath      { get; set; } = "HurtArea";  // พื้นที่ "โดนตัว"

	[Export] public bool  ClampToViewport = false;
	[Export] public float ClampMargin     = 8f;

	private AnimatedSprite2D _anim;
	private Area2D _mouthArea;
	private Area2D _hurtArea;
	private float _biteTimer;

	// เก็บ Fish ที่อยู่ในปาก
	private readonly HashSet<Fish> _targetsInMouth = new();

	// ===== helper หา ScoreManager แบบกันพลาด =====
	private ScoreManager GetSM()
	{
		return
			GetNodeOrNull<ScoreManager>("%ScoreManager") ??
			GetNodeOrNull<ScoreManager>("/root/Main/ScoreManager") ??
			GetNodeOrNull<ScoreManager>("/root/ScoreManager");
	}

	// ===== helper แปลง Node/Area2D -> Fish =====
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

	public override void _Ready()
	{
		_anim      = GetNodeOrNull<AnimatedSprite2D>(AnimatedSpritePath);
		_mouthArea = GetNodeOrNull<Area2D>(MouthAreaPath);
		_hurtArea  = GetNodeOrNull<Area2D>(HurtAreaPath);

		if (_anim != null && _anim.SpriteFrames?.HasAnimation(SwimAnimation) == true)
			_anim.Play(SwimAnimation);

		// พื้นที่ "งับ"
		if (_mouthArea != null)
		{
			_mouthArea.Monitoring  = true;
			_mouthArea.Monitorable = true;

			_mouthArea.BodyEntered += body => { if (TryGetFish(body, out var fish)) _targetsInMouth.Add(fish); };
			_mouthArea.BodyExited  += body => { if (TryGetFish(body, out var fish)) _targetsInMouth.Remove(fish); };

			_mouthArea.AreaEntered += area => { if (TryGetFish(area, out var fish)) _targetsInMouth.Add(fish); };
			_mouthArea.AreaExited  += area => { if (TryGetFish(area, out var fish)) _targetsInMouth.Remove(fish); };
		}

		// พื้นที่ "โดนตัว"
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
		if (sm != null && sm.IsGameOver) return;

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
	}

	private Vector2 GetMoveInput()
	{
		float x = Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left");
		float y = Input.GetActionStrength("ui_down")  - Input.GetActionStrength("ui_up");
		var v = new Vector2(x, y);
		return v == Vector2.Zero ? v : v.Normalized();
	}

	// ===== งับเพื่อกิน (ต้องกดปุ่ม) =====
	private void TryBite()
	{
		var sm = GetSM();
		if (sm != null && sm.IsGameOver) return;

		if (_biteTimer > 0f) return;
		_biteTimer = BiteCooldown;

		if (_anim != null && _anim.SpriteFrames?.HasAnimation(BiteAnimation) == true)
			_anim.Play(BiteAnimation);

		var toCheck = new List<Fish>(_targetsInMouth);
		foreach (var fish in toCheck)
		{
			if (!IsInstanceValid(fish)) { _targetsInMouth.Remove(fish); continue; }

			// คะแนนไม่ถึงปลาตัวนี้ → ตาย
			if (sm != null && sm.Score < fish.RequiredScore)
			{
				GD.Print($"[Player] Too small to bite {fish.Name}: need {fish.RequiredScore}, have {sm.Score} → DIE");
				sm.PlayerDied();
				return;
			}

			// กินได้ → บวกคะแนน
			sm?.Add(fish.Points);
			GD.Print($"[Player] Eat {fish.Name}, +{fish.Points}");

			fish.QueueFree();
			_targetsInMouth.Remove(fish);
			break; // งับครั้งเดียวกิน 1 ตัว
		}

		if (_anim != null && _anim.SpriteFrames?.HasAnimation(SwimAnimation) == true)
			_anim.Play(SwimAnimation);
	}

	// ===== โดนตัวปลาใหญ่เมื่อไหร่ ตายทันที (ไม่ต้องกด) =====
	private void CheckDeathOnTouch(Fish fish)
	{
		var sm = GetSM();
		if (sm == null || sm.IsGameOver) return;

		if (sm.Score < fish.RequiredScore)
		{
			GD.Print($"[Player] Touch big {fish.Name}: need {fish.RequiredScore}, have {sm.Score} → DIE");
			sm.PlayerDied();
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
}
