using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
	// ===== Layers (bit masks) =====
	private const uint L_PLAYER  = 1u << 0; // 1
	private const uint L_MOUTH   = 1u << 1; // 2
	private const uint L_HURT    = 1u << 2; // 4
	private const uint L_FISH    = 1u << 3; // 8
	private const uint L_CRYSTAL = 1u << 5; // 32
	private const uint L_COIN    = 1u << 6; // 64

	// ===== CONFIG =====
[Export] public float MaxSpeed = 220f;       // ความเร็วสูงสุดของผู้เล่น
[Export] public float Accel    = 900f;       // ค่าความเร่งเมื่อกดเคลื่อน
[Export] public float Friction = 900f;       // ความฝืดเมื่อหยุดเคลื่อน
[Export] public float BiteCooldown = 0.35f;  // เวลาคูลดาวน์ของการกัด
[Export] public float BiteReach = 90f;       // ระยะกัดจากปากของปลา

[Export] public string SwimAnimation = "swim"; // ชื่ออนิเมชันว่ายน้ำ
[Export] public string BiteAnimation = "bite"; // ชื่ออนิเมชันตอนกัด

// NodePath ใช้เชื่อมกับโหนดในฉาก
[Export] public NodePath AnimatedSpritePath { get; set; } = "AnimatedSprite2D";
[Export] public NodePath MouthAreaPath     { get; set; } = "MouthArea";
[Export] public NodePath HurtAreaPath      { get; set; } = "HurtArea";

[Export] public bool  ClampToViewport = false; // จำกัดไม่ให้ออกนอกขอบจอ
[Export] public float ClampMargin     = 8f;    // ระยะกันขอบหน้าจอ

// ===== Grace / Invulnerability =====
[Export] public float SpawnGraceSeconds { get; set; } = 1.2f; // เวลาปลอดภัยหลังเกิดใหม่
private float _spawnGraceTimer = 0f; // ตัวนับเวลาคุ้มกัน

// ===== Debug settings =====
[Export] public bool AutoBiteWhenPossible = true;  // ให้กัดอัตโนมัติเมื่อกัดได้
[Export] public bool DebugBypassBiteChecks = true; // ข้ามการตรวจเงื่อนไขกัด (ดีบัก)

// กันตรวจ Hurt ระยะไกลเกินจริง
[Export] public float HurtTriggerDistance = 28f;

// เก็บ ID ของคริสตัลที่ถูกเก็บแล้วกันซ้ำ
private readonly HashSet<ulong> _pickedCrystalOnce = new();

// ===== ตัวแปรภายใน =====
private AnimatedSprite2D _anim; // อนิเมชันหลักของผู้เล่น
private Area2D _mouthArea;      // โซนตรวจการกัด
private Area2D _hurtArea;       // โซนตรวจโดนปลาใหญ่
private float _biteTimer;       // ตัวจับเวลาคูลดาวน์กัด

private readonly HashSet<Fish> _targetsInMouth = new(); // ปลาที่อยู่ในระยะปาก

private float _baseMaxSpeed;    // เก็บค่า speed เดิมไว้ใช้รีเซ็ต
private float _biteCooldownBase; // เก็บ cooldown เดิม
private Color _originalModulate = Colors.White; // สีดั้งเดิมของผู้เล่น

// ===== สกิลและสถานะ =====
private bool _berserk, _magnet, _phase; // สถานะสกิลต่างๆ
private int  _shieldStacks;              // จำนวนชั้นโล่
[Export] public int MaxShieldStacks = 99; // โล่สูงสุดที่ถือได้
private float _magnetRadius;            // รัศมีดูดของแม่เหล็ก
private double _timeScaleBefore = 1.0;  // ค่าความเร็วเวลาปกติ
private bool _hurtAreaWasMonitoring = true; // จำสถานะการตรวจชนก่อน Phase

// ===== เก็บคริสตัลแบบกันพัง =====
[Export] public float CrystalPickupRadius = 60f; // ระยะดูดคริสตัลอัตโนมัติ

// ===== Utils หา Manager =====
private ScoreManager GetSM() => ScoreManager.Instance; // เรียก ScoreManager (Autoload)
private SkillManager GetSKM()
{
	var skm = GetNodeOrNull<SkillManager>("%SkillManager"); // หาจาก scene ปัจจุบัน
	if (skm != null) return skm;
	return GetTree().CurrentScene?.GetNodeOrNull<SkillManager>("SkillManager")
		?? GetTree().Root.GetNodeOrNull<SkillManager>("SkillManager"); // ถ้าไม่เจอ หาใน root
}

// ===== Crystal helpers =====
private bool IsCrystalNode(Node n)
{
	if (n == null) return false;
	if (n.IsInGroup("Crystal") || n is CrystalPickup) return true; // ถ้าอยู่ในกลุ่มหรือเป็น CrystalPickup
	var nm = n.Name.ToString().ToLowerInvariant();
	return nm.Contains("crystal") || nm.Contains("gem") || nm.Contains("pickup"); // ตรวจชื่อ
}

// หา CrystalPickup ตัวจริงจากโหนดลูก
private CrystalPickup ResolveCrystal(Node n)
{
	if (n is CrystalPickup cp) return cp;             // ตัวเองเป็นคริสตัล
	if (n.GetParent() is CrystalPickup p1) return p1; // พ่อเป็นคริสตัล
	if (n.GetOwner()  is CrystalPickup p2) return p2; // owner เป็นคริสตัล
	return n.GetParent()?.GetParent() as CrystalPickup; // ลึกไปอีกชั้น
}
private bool TryGetCrystal(Node n, out CrystalPickup cp) { cp = ResolveCrystal(n); return cp != null; }

// เก็บคริสตัลแบบ deferred เพื่อไม่ให้ระบบตรวจชนค้าง
private void EatCrystal(CrystalPickup cp)
{
	if (cp != null && IsInstanceValid(cp))
		cp.CallDeferred("CollectBy", this); // เรียกเก็บคริสตัลทีหลังในคิว
}

// เรียกใช้สกิลผ่าน SkillManager แบบ deferred
private void ApplyCrystalDeferred(string id, float dur)
{
	GetSKM()?.Apply(id, dur); // ส่งรหัสสกิลกับเวลาไปให้ระบบสกิลจัดการ
}

	// Fallback: guess crystal id from node name/meta and apply
	private string GuessCrystalId(Node n)
	{
		if (n == null) return "";
		if (n.HasMeta("crystal_id")) return n.GetMeta("crystal_id").ToString();
		if (n.HasMeta("crystal_color")) return n.GetMeta("crystal_color").ToString();

		string name = n.Name.ToString().ToLowerInvariant();
		if (name.Contains("blue"))  return "Blue";
		if (name.Contains("green")) return "Green";
		if (name.Contains("pink"))  return "Pink";
		if (name.Contains("redadd") || name.Contains("red_plus") || name.Contains("red+") || name.Contains("time+") || name.Contains("addtime")) return "RedAdd";
		if (name.Contains("redsub") || name.Contains("red_minus") || name.Contains("red-") || name.Contains("time-") || name.Contains("subtime")) return "RedSub";
		if (name.Contains("red")) return "RedAdd";
		return "";
	}

	// หา "ราก" ของอินสแตนซ์คริสตัลจริง ๆ (ไม่ไปหยิบคอนเทนเนอร์รวม)
private Node GetCrystalDedupRoot(Node n)
{
	if (n == null) return null;

	// ถ้าโหนดนี้เป็น CrystalPickup เอง ก็ใช้มันเลย
	if (n is CrystalPickup) return n;

	// ถ้าพ่อเป็น CrystalPickup ก็ใช้พ่อนั่นแหละ
	if (n.GetParent() is CrystalPickup p1) return p1;

	// ใช้ Owner (ท็อปของ PackedScene อินสแตนซ์) เฉพาะกรณี owner เป็น CrystalPickup เท่านั้น
	var owner = n.GetOwner();
	if (owner is CrystalPickup) return owner;

	// ไม่เจออะไร ก็ตัวมันเอง (อย่าไล่ขึ้นไปจับโหนดชื่อ CrystalRoot)
	return n;
}

	private bool TryPickupCrystalNode(Node n)
{
	if (n == null) return false;

	Node root = GetCrystalDedupRoot(n) ?? n;

	// ถ้าชิ้นนี้ถูกเก็บไปแล้ว ก็ไม่ต้องทำซ้ำ
	if (root.HasMeta("_picked")) return false;
	if (_pickedCrystalOnce.Contains(root.GetInstanceId())) return false;

	// ทำเครื่องหมายว่าชิ้นนี้ถูกเก็บแล้ว
	_pickedCrystalOnce.Add(root.GetInstanceId());
	root.SetMeta("_picked", true);

	// มีสคริปต์ CrystalPickup → เก็บแบบ deferred
	if (TryGetCrystal(n, out var cp))
	{
		EatCrystal(cp);
		return true;
	}

	// fallback: เดาสีแล้ว Apply + ลบ
	string idStr = GuessCrystalId(n);
	if (!string.IsNullOrEmpty(idStr))
	{
		CallDeferred(nameof(ApplyCrystalDeferred), idStr, -1f);
		if (IsInstanceValid(root)) root.CallDeferred("queue_free");
		return true;
	}

	return false;
}

	// ===== Coin helpers =====
	private Coin ResolveCoin(Node n)
	{
		if (n is Coin c) return c;
		if (n.GetParent() is Coin pc) return pc;
		if (n.GetOwner()  is Coin oc) return oc;
		return n.GetParent()?.GetParent() as Coin;
	}
	private bool TryGetCoin(Node n, out Coin coin) { coin = ResolveCoin(n); return coin != null; }
	private void EatCoin(Coin coin) { if (coin != null && IsInstanceValid(coin)) coin.Consume(); }

	// ===== Fish resolve (ไม่ต้องพึ่ง group) =====
	private Fish ResolveFish(Node n)
	{
		Node cur = n;
		for (int i = 0; i < 6 && cur != null; i++)
		{
			if (cur is Fish f) return f;
			cur = cur.GetParent();
		}
		if (n.GetOwner() is Fish of) return of;
		return null;
	}
	private bool TryGetFish(Node n, out Fish fish) { fish = ResolveFish(n); return fish != null; }

	// ===== Debug helpers =====
	private void DebugLogEnter(string tag, Node other)
	{
		string name = other?.Name?.ToString() ?? "null";
		string t = other?.GetType()?.Name ?? "null";
		GD.Print($"[DEBUG] {tag} enter -> {name} ({t})");
	}
	private void DebugLogExit(string tag, Node other)
	{
		string name = other?.Name?.ToString() ?? "null";
		string t = other?.GetType()?.Name ?? "null";
		GD.Print($"[DEBUG] {tag} exit -> {name} ({t})");
	}

	// ===== Ready =====
	public override void _Ready()
	{
		ProcessMode = Node.ProcessModeEnum.Inherit;
		_anim      = GetNodeOrNull<AnimatedSprite2D>(AnimatedSpritePath);
		_mouthArea = GetNodeOrNull<Area2D>(MouthAreaPath);
		_hurtArea  = GetNodeOrNull<Area2D>(HurtAreaPath);

		_biteCooldownBase = BiteCooldown;
		_baseMaxSpeed     = MaxSpeed;
		_originalModulate = Modulate;

		if (_anim != null && _anim.SpriteFrames?.HasAnimation(SwimAnimation) == true)
			_anim.Play(SwimAnimation);

		// บังคับ Layer/Mask กันพลาด
		CollisionLayer = L_PLAYER;
		CollisionMask  = L_FISH;

		if (_mouthArea != null)
		{
			_mouthArea.CollisionLayer = L_MOUTH;
			_mouthArea.CollisionMask  = L_FISH | L_CRYSTAL | L_COIN;
			_mouthArea.Monitoring = true;
			_mouthArea.Monitorable = true;

			_mouthArea.BodyEntered += body => {
				DebugLogEnter("Mouth-Body", body);
				if (TryGetFish(body, out var fish)) _targetsInMouth.Add(fish);
				if (TryGetCoin(body, out var coin)) EatCoin(coin);
				if (TryPickupCrystalNode(body)) { }
			};
			_mouthArea.BodyExited += body => {
				DebugLogExit("Mouth-Body", body);
				if (TryGetFish(body, out var fish)) _targetsInMouth.Remove(fish);
			};
			_mouthArea.AreaEntered += area => {
				DebugLogEnter("Mouth-Area", area);
				if (TryGetFish(area, out var fish)) _targetsInMouth.Add(fish);
				if (TryGetCoin(area, out var coin)) EatCoin(coin);
				if (TryPickupCrystalNode(area)) { }
			};
			_mouthArea.AreaExited += area => {
				DebugLogExit("Mouth-Area", area);
				if (TryGetFish(area, out var fish)) _targetsInMouth.Remove(fish);
			};
		}

		if (_hurtArea != null)
		{
			_hurtArea.CollisionLayer = L_HURT;
			_hurtArea.CollisionMask  = L_FISH; // ไม่เห็น 6/7
			_hurtArea.Monitoring = true;
			_hurtArea.Monitorable = true;

			_hurtArea.BodyEntered += body => { DebugLogEnter("Hurt-Body", body); OnHurtEnter(body); };
			_hurtArea.AreaEntered += area => { DebugLogEnter("Hurt-Area", area); OnHurtEnter(area); };
		}

		AddToGroup("Player");
		_spawnGraceTimer = SpawnGraceSeconds;
		// safety: ensure practical distances
		if (BiteReach < 60f) BiteReach = 100f;
		if (HurtTriggerDistance < 60f) HurtTriggerDistance = 100f; // นิรภัยเริ่มเกม
	}

	private void OnHurtEnter(Node other)
{
	// 1) กันช่วงเกิดใหม่/กำลังไร้ตัวตน/ชนคริสตัล
	if (_spawnGraceTimer > 0f || _phase) return;
	if (IsCrystalNode(other)) return;

	// 2) ต้องเป็นปลา และเกมยังไม่จบ
	if (!TryGetFish(other, out var fish) || !IsInstanceValid(fish)) return;
	var sm = GetSM();
	if (sm == null || sm.IsLevelCleared || sm.IsGameOver) return;

	// 3) ถ้าคะแนนเราน้อยกว่า RequiredScore ของปลา → โดนทันที
	if (sm.LevelScore < fish.RequiredScore)
	{
		AttemptDieOrSpendShield(fish, "Touch bigger fish");
		return;
	}

	// ถ้าคะแนนพอแล้วก็ไม่โดน (ปลาตัวนี้ถือว่า “เล็กพอจะกินได้”)
}

	// ===== Physics =====
	public override void _PhysicsProcess(double delta)
	{
		if (GetTree().Paused) return;

		var sm = GetSM();
		if (sm != null && (sm.IsLevelCleared || sm.IsGameOver)) return;

		if (_spawnGraceTimer > 0f) _spawnGraceTimer -= (float)delta;

		Vector2 input = GetMoveInput();
		Vector2 target = input * MaxSpeed;
		Velocity = (input != Vector2.Zero)
			? Velocity.MoveToward(target, Accel * (float)delta)
			: Velocity.MoveToward(Vector2.Zero, Friction * (float)delta);
		MoveAndSlide();

		if (_anim != null && MathF.Abs(Velocity.X) > 1f)
			_anim.FlipH = Velocity.X < 0f;

		if (ClampToViewport) ClampInsideViewport();

		if (_biteTimer > 0f) _biteTimer -= (float)delta;
		if (Input.IsActionJustPressed("bite") || Input.IsActionJustPressed("ui_accept"))
			TryBite();

		SweepCollectNearbyCrystals();
		UpdateSizeBasedOnScore(sm);

		if (AutoBiteWhenPossible && sm != null)
		{
			foreach (var f in _targetsInMouth)
			{
				if (IsInstanceValid(f) && sm.LevelScore >= f.RequiredScore)
				{
					TryBite();
					break;
				}
			}
		}
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

		Vector2 mouthPos = (_mouthArea as Node2D)?.GlobalPosition ?? GlobalPosition;

		foreach (var fish in new List<Fish>(_targetsInMouth))
		{
			if (!IsInstanceValid(fish)) { _targetsInMouth.Remove(fish); continue; }

			// QUICK BYPASS: eat anything inside mouth when flag is on (for debugging/exhausted mode)
			if (!DebugBypassBiteChecks)
			{
				Vector2 mouthPos2 = (_mouthArea as Node2D)?.GlobalPosition ?? GlobalPosition;
				if (fish.GlobalPosition.DistanceTo(mouthPos2) > BiteReach) { continue; }
				if (sm != null && sm.LevelScore < fish.RequiredScore) { continue; }
			}

			int gained = CalcBiteScore(fish.Points);
			sm?.AddScore(gained, fish.FishType);
			fish.OnEaten();
			_targetsInMouth.Remove(fish);
			break;
		}

		if (_anim != null && _anim.SpriteFrames?.HasAnimation(SwimAnimation) == true)
			_anim.Play(SwimAnimation);
	}

	private void AttemptDieOrSpendShield(Fish source, string reason)
	{
		var sm = GetSM();
		if (sm == null) return;

		if (_shieldStacks > 0)
		{
			_shieldStacks--;

			// --- clear HUD when last green shield is consumed ---
			if (_shieldStacks <= 0)
			{
				var hud = GetTree().CurrentScene?.FindChild("CrystalHud", true, false) as CrystalHud
						  ?? GetTree().Root.GetNodeOrNull<CrystalHud>("CrystalHud");
				hud?.ClearBuff(CrystalType.Green);
			}

			ShowShieldFx(_shieldStacks > 0);
			return;
		}

		sm.LoseLife(1);
		_spawnGraceTimer = Math.Max(_spawnGraceTimer, SpawnGraceSeconds);
		_targetsInMouth.Clear();
		Modulate = new Color(1, 1, 1, 0.85f);
		GetTree().CreateTimer(SpawnGraceSeconds).Timeout += () => Modulate = _originalModulate;
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
		float targetScale = 1.0f;
		if (sm.LevelScore >= 150) targetScale = 2.4f;
		else if (sm.LevelScore >= 20) targetScale = 1.9f;
		else if (sm.LevelScore >= 10) targetScale = 1.3f;
		Scale = Scale.Lerp(Vector2.One * targetScale, 0.05f);
	}

	private void SweepCollectNearbyCrystals()
{
	Vector2 mouthPos = GlobalPosition;
	if (_mouthArea is Node2D ma) mouthPos = ma.GlobalPosition;

	var all = new List<Node>(GetTree().GetNodesInGroup("Crystal"));

	foreach (var n in all)
	{
		if (n is not Node2D node || !IsInstanceValid(node)) continue;
		if (node.GlobalPosition.DistanceTo(mouthPos) > CrystalPickupRadius) continue;

		// พยายามเก็บแบบมีสคริปต์ก่อน
		if (TryPickupCrystalNode(node)) continue;

		// fallback: ถ้าไม่มีสคริปต์ ก็แปลงเป็นชมพูสั้น ๆ แล้วลบทิ้ง
		CallDeferred(nameof(ApplyCrystalDeferred), "Pink", -1f);
		var root = GetCrystalDedupRoot(node) ?? node;
		if (IsInstanceValid(root)) root.CallDeferred("queue_free");
	}
	// ไม่มี return ต้นฟังก์ชันอีกต่อไป → เฟรมเดียวเก็บได้หลายชิ้น
}

	private int  CalcBiteScore(int baseScore) => _berserk ? (int)MathF.Round(baseScore * 2f) : baseScore;
	private void ShowShieldFx(bool enabled) { /* optional VFX */ }

	// ===== Public APIs (สกิล) =====
	public void SetTimeFreeze(bool on, float worldScale = 0.35f)
	{
		if (on)
		{
			_timeScaleBefore = Engine.TimeScale;
			Engine.TimeScale  = Math.Clamp(worldScale, 0.1f, 1f);
		}
		else Engine.TimeScale = (_timeScaleBefore <= 0.0) ? 1.0 : _timeScaleBefore;
	}

	public void GiveThornShield(int stacks)
	{
		int add = Math.Max(1, stacks);
		if (MaxShieldStacks > 0)
			_shieldStacks = Math.Min(_shieldStacks + add, MaxShieldStacks);
		else
			_shieldStacks += add;
		ShowShieldFx(_shieldStacks > 0);
		var sm = GetNodeOrNull<SkillManager>("%SkillManager");
		if (sm != null) sm.OnShieldConsumed();
	}

	public void RemoveThornIfAny() { _shieldStacks = 0; ShowShieldFx(false); }

	public void SetMagnet(bool on, float radius = 260f, float speedBoost = 0.15f)
	{
		_magnet = on;
		_magnetRadius = on ? radius : 0f;
		MaxSpeed = _baseMaxSpeed * (on ? (1f + speedBoost) : 1f);
	}

	public void SetPhase(bool on)
	{
		_phase = on;
		if (_hurtArea != null)
		{
			if (on) { _hurtAreaWasMonitoring = _hurtArea.Monitoring; _hurtArea.Monitoring = false; }
			else    { _hurtArea.Monitoring = _hurtAreaWasMonitoring; }
		}
		Modulate = on ? new Color(1,1,1,0.7f) : _originalModulate;
	}
}
