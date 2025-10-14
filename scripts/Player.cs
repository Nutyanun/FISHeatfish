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
	[Export] public NodePath MouthAreaPath { get; set; } = "MouthArea";
	[Export] public NodePath HurtAreaPath { get; set; } = "HurtArea";

	// === การจำกัดขอบจอ ===
	[Export] public bool ClampToViewport = false;
	[Export] public float ClampMargin = 8f;

	// === ตัวแปรภายใน ===
	private AnimatedSprite2D _anim;
	private Area2D _mouthArea;
	private Area2D _hurtArea;
	private float _biteTimer;

	// === สถานะสกิล ===
	private bool _berserk, _magnet, _phase;
	private int _shieldStacks;
	private float _biteCooldownBase;
	private float _baseMaxSpeed;
	private float _magnetRadius;
	private double _timeScaleBefore = 1.0;

	// === สำหรับ Phase (จำสถานะเดิมของ HurtArea) ===
	private bool _hurtAreaWasMonitoring = true;
	private Color _originalModulate = Colors.White;

	// === set เก็บเป้าหมายที่อยู่ในปาก ===
	private readonly HashSet<Fish> _targetsInMouth = new();

	// ===========================================
	// 🟦 ส่วน Helper: หา Node อื่น และ Resolve Object
	// ===========================================
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
		coin.Consume();
	}

	// ====== รองรับ Crystal ======
	private CrystalPickup ResolveCrystal(Node n)
	{
		if (n is CrystalPickup cp) return cp;
		if (n.GetParent() is CrystalPickup p1) return p1;
		if (n.GetOwner() is CrystalPickup p2) return p2;
		return n.GetParent()?.GetParent() as CrystalPickup;
	}

	private bool TryGetCrystal(Node n, out CrystalPickup crystal)
	{
		crystal = ResolveCrystal(n);
		return crystal != null;
	}

	private void EatCrystal(CrystalPickup cp)
	{
		if (cp == null || !IsInstanceValid(cp)) return;
		cp.CollectBy(this);
	}

	// ===========================================
	// 🟩 _Ready(): Setup ทุกอย่าง
	// ===========================================
	public override void _Ready()
	{
		 // ✅ ให้ Player หยุดตาม pause เสมอ
	ProcessMode = Node.ProcessModeEnum.Inherit;
		_anim = GetNodeOrNull<AnimatedSprite2D>(AnimatedSpritePath);
		_mouthArea = GetNodeOrNull<Area2D>(MouthAreaPath);
		_hurtArea = GetNodeOrNull<Area2D>(HurtAreaPath);

		_biteCooldownBase = BiteCooldown;
		_baseMaxSpeed = MaxSpeed;
		_originalModulate = Modulate;

		if (_anim != null && _anim.SpriteFrames?.HasAnimation(SwimAnimation) == true)
			_anim.Play(SwimAnimation);

		// --- Mouth Area ---
		if (_mouthArea != null)
		{
			_mouthArea.Monitoring = true;
			_mouthArea.Monitorable = true;

			_mouthArea.BodyEntered += body => { if (TryGetFish(body, out var fish)) _targetsInMouth.Add(fish); };
			_mouthArea.BodyExited += body => { if (TryGetFish(body, out var fish)) _targetsInMouth.Remove(fish); };
			_mouthArea.AreaEntered += area => { if (TryGetFish(area, out var fish)) _targetsInMouth.Add(fish); };
			_mouthArea.AreaExited += area => { if (TryGetFish(area, out var fish)) _targetsInMouth.Remove(fish); };

			_mouthArea.BodyEntered += body => { if (TryGetCoin(body, out var coin)) EatCoin(coin); };
			_mouthArea.AreaEntered += area => { if (TryGetCoin(area, out var coin)) EatCoin(coin); };

			_mouthArea.BodyEntered += body => { if (TryGetCrystal(body, out var cp)) EatCrystal(cp); };
			_mouthArea.AreaEntered += area => { if (TryGetCrystal(area, out var cp)) EatCrystal(cp); };
		}

		// --- Hurt Area ---
		if (_hurtArea != null)
		{
			_hurtArea.Monitoring = true;
			_hurtArea.Monitorable = true;

			_hurtArea.BodyEntered += body => { if (TryGetFish(body, out var fish)) CheckDeathOnTouch(fish); };
			_hurtArea.AreaEntered += area => { if (TryGetFish(area, out var fish)) CheckDeathOnTouch(fish); };
		}
	}

	// ===========================================
	// 🟨 _PhysicsProcess(): อัปเดตการเคลื่อนไหว + กัด + Magnet
	// ===========================================
	public override void _PhysicsProcess(double delta)
	{
		// ถ้าเกมหยุดชั่วคราว → อย่าขยับ
		if (GetTree().Paused)
		return;
		
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

		if (_magnet && _magnetRadius > 0f)
			PullCoinsTowardSelf((float)delta);

		UpdateSizeBasedOnScore(sm);
	}

	private Vector2 GetMoveInput()
	{
		float x = Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left");
		float y = Input.GetActionStrength("ui_down") - Input.GetActionStrength("ui_up");
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

		if (_anim != null && _anim.SpriteFrames?.HasAnimation(BiteAnimation) == true)
			_anim.Play(BiteAnimation);

		var toCheck = new List<Fish>(_targetsInMouth);
		foreach (var fish in toCheck)
		{
			if (!IsInstanceValid(fish)) { _targetsInMouth.Remove(fish); continue; }

			if (sm != null && sm.LevelScore < fish.RequiredScore)
			{
				AttemptDieOrSpendShield(fish, reason: "Too small to bite");
				return;
			}

			int gained = CalcBiteScore(fish.Points);
			sm?.AddScore(gained, fish.FishType);

			fish.OnEaten();
			GD.Print($"[Player] Eat {fish.FishType}, +{gained}");

			if (_berserk)
				ChainBiteNearby(fish.GlobalPosition, 60f, maxChain: 2);

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
		if (_phase) return;

		if (sm.LevelScore < fish.RequiredScore)
			AttemptDieOrSpendShield(fish, reason: "Touch bigger fish");
	}

	private void AttemptDieOrSpendShield(Fish source, string reason)
	{
		var sm = GetSM();
		if (sm == null) return;

		if (_shieldStacks > 0)
		{
			_shieldStacks--;
			ShowShieldFx(_shieldStacks > 0);
			if (IsInstanceValid(source))
			{
				Vector2 dir = (source.GlobalPosition - GlobalPosition).Normalized();
				source.GlobalPosition += dir * 24f;
			}
			GD.Print($"[Player] Shield saved life ({reason}). Stacks left: {_shieldStacks}");
			return;
		}

		GD.Print($"[Player] {reason} → DIE");
		sm.LoseLife(1);
	}

	private void ClampInsideViewport()
	{
		var rect = GetViewportRect();
		float minX = rect.Position.X + ClampMargin;
		float maxX = rect.End.X - ClampMargin;
		float minY = rect.Position.Y + ClampMargin;
		float maxY = rect.End.Y - ClampMargin;

		GlobalPosition = new Vector2(
			Mathf.Clamp(GlobalPosition.X, minX, maxX),
			Mathf.Clamp(GlobalPosition.Y, minY, maxY)
		);
	}

	private void UpdateSizeBasedOnScore(ScoreManager sm)
	{
		if (sm == null) return;

		float targetScale = 1.0f;

		if (sm.LevelScore >= 30)
			targetScale = 2.4f;
		else if (sm.LevelScore >= 15)
			targetScale = 1.9f;
		else if (sm.LevelScore >= 10)
			targetScale = 1.3f;

		Scale = Scale.Lerp(Vector2.One * targetScale, 0.05f);
	}

	// กัดลาม: หา "ปลา" ใกล้ตำแหน่งที่เพิ่งกัด
	private void ChainBiteNearby(Vector2 at, float radius = 60f, int maxChain = 2)
	{
		if (!_berserk) return;

		int chain = 0;
		foreach (var n in GetTree().GetNodesInGroup("fish"))
		{
			if (chain >= maxChain) break;
			if (n is not Fish f || !IsInstanceValid(f)) continue;

			if (f.GlobalPosition.DistanceTo(at) <= radius)
			{
				var sm = GetSM();
				if (sm == null) break;

				if (sm.LevelScore >= f.RequiredScore)
				{
					int gained = CalcBiteScore(f.Points);
					sm.AddScore(gained, f.FishType);
					f.OnEaten();
					chain++;
					GD.Print($"[Player] Chain bite {f.FishType}, +{gained}");
				}
			}
		}
	}

	private int CalcBiteScore(int baseScore) => _berserk ? (int)MathF.Round(baseScore * 2f) : baseScore;
	private void ShowShieldFx(bool enabled) { }
	private void PullCoinsTowardSelf(float delta)
	{
		const float pullSpeed = 900f;
		foreach (var n in GetTree().GetNodesInGroup("coins"))
		{
			if (n is not Node2D node || !IsInstanceValid(node)) continue;
			float dist = node.GlobalPosition.DistanceTo(GlobalPosition);
			if (dist <= _magnetRadius)
			{
				Vector2 dir = (GlobalPosition - node.GlobalPosition).Normalized();
				node.GlobalPosition += dir * pullSpeed * delta;
			}
		}
	}
	// =====================================================
// ===== 🔮 Skill Methods used by SkillManager.cs =====
// =====================================================

// — Time Freeze: ชะลอเวลา (ลดความเร็วทั้งเกม)
public void SetTimeFreeze(bool on, float worldScale = 0.35f)
{
	if (on)
	{
		_timeScaleBefore = Engine.TimeScale;  // จำค่าสปีดเดิมไว้
		float clamped = Mathf.Clamp(worldScale, 0.1f, 1f);
		Engine.TimeScale = (double)clamped;   // ลด TimeScale ของโลกทั้งเกม
		GD.Print($"[Player] Time Freeze ON (scale={clamped})");
	}
	else
	{
		Engine.TimeScale = (_timeScaleBefore <= 0.0) ? 1.0 : _timeScaleBefore;
		GD.Print("[Player] Time Freeze OFF");
	}
}

// — Thorn Shield: โล่กันตาย (ซ้อนสูงสุด 1 ชั้น)
public void GiveThornShield(int stacks)
{
	_shieldStacks = Math.Min(_shieldStacks + stacks, 1);
	ShowShieldFx(_shieldStacks > 0);
	GD.Print($"[Player] Shield applied ({_shieldStacks} stack)");
}

// — เอาโล่ออก (เมื่อหมดเวลา)
public void RemoveThornIfAny()
{
	_shieldStacks = 0;
	ShowShieldFx(false);
	GD.Print("[Player] Shield removed");
}

// — Magnet: ดูดเหรียญและเพิ่มความเร็วชั่วคราว
public void SetMagnet(bool on, float radius = 260f, float speedBoost = 0.15f)
{
	_magnet = on;
	_magnetRadius = on ? radius : 0f;
	MaxSpeed = _baseMaxSpeed * (on ? (1f + speedBoost) : 1f);
	GD.Print($"[Player] Magnet {(on ? "ON" : "OFF")}");
}

// — Phase: ทะลุผ่านศัตรูได้ (ปิด HurtArea + โปร่งแสง)
public void SetPhase(bool on)
{
	_phase = on;

	if (_hurtArea != null)
	{
		if (on)
		{
			_hurtAreaWasMonitoring = _hurtArea.Monitoring;
			_hurtArea.Monitoring = false;
		}
		else
		{
			_hurtArea.Monitoring = _hurtAreaWasMonitoring;
		}
	}

	Modulate = on ? new Color(1, 1, 1, 0.7f) : _originalModulate;
	GD.Print($"[Player] Phase {(on ? "ON" : "OFF")}");
}
}
