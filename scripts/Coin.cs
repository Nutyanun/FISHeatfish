using Godot;

public partial class Coin : CharacterBody2D
{
	public enum CoinType { Bronze, Silver, Gold }

	[Signal] public delegate void EatenEventHandler(int value);
	[Signal] public delegate void DespawnedEventHandler();

	[Export] public CoinType Type { get; set; } = CoinType.Bronze;

	[Export] public int BronzeValue { get; set; } = 100;
	[Export] public int SilverValue { get; set; } = 250;
	[Export] public int GoldValue   { get; set; } = 500;

	[Export] public float FallSpeedMin   { get; set; } = 180f;
	[Export] public float FallSpeedMax   { get; set; } = 280f;
	[Export] public float DriftXAmplitude{ get; set; } = 24f;  // แกว่งซ้าย-ขวา
	[Export] public float DriftXSpeed    { get; set; } = 2.2f; // เร็วการแกว่ง (rad/s)
	[Export] public float RotateSpeedRad { get; set; } = 0f;   // จะหมุนด้วยก็ได้
	[Export] public float DespawnMargin  { get; set; } = 64f;  // หลุดขอบล่างแล้วลบ

	private float _fallSpeed;
	private float _t;
	private float _startX;

	private Area2D _hitbox;                 // ต้องมีลูกชื่อ "Hitbox" + CollisionShape2D
	private AnimatedSprite2D _anim;         // (ไม่บังคับ) ลูกชื่อ "AnimatedSprite2D"

	public int Value => Type switch
	{
		CoinType.Bronze => BronzeValue,
		CoinType.Silver => SilverValue,
		CoinType.Gold   => GoldValue,
		_ => 0
	};

	public override void _Ready()
	{
		_hitbox = GetNodeOrNull<Area2D>("Hitbox");
		if (_hitbox != null)
		{
			_hitbox.Monitoring  = true;
			_hitbox.Monitorable = true;
			_hitbox.AreaEntered += OnHitboxAreaEntered;
			_hitbox.BodyEntered += OnHitboxBodyEntered;
		}

		_anim = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (_anim != null)
		{
			var animName = Type.ToString(); // "Bronze"/"Silver"/"Gold"
			if (_anim.SpriteFrames != null && _anim.SpriteFrames.HasAnimation(animName))
				_anim.Play(animName);
			else
				_anim.Play();
		}

		_fallSpeed = (float)GD.RandRange(FallSpeedMin, FallSpeedMax); // double -> float
		_t         = (float)GD.RandRange(0.0, 100.0);
		_startX    = GlobalPosition.X;
	}

	public override void _PhysicsProcess(double delta)
	{
		// ตกลงด้วยฟิสิกส์
		Velocity = new Vector2(0f, _fallSpeed);
		MoveAndSlide();

		// แกว่งซ้าย-ขวา
		_t += (float)delta * DriftXSpeed;
		float x = _startX + Mathf.Sin(_t) * DriftXAmplitude;
		GlobalPosition = new Vector2(x, GlobalPosition.Y);

		if (RotateSpeedRad != 0f)
			Rotation += RotateSpeedRad * (float)delta;

		// หลุดจอล่าง → ลบ
		var r = GetViewportRect();
		if (GlobalPosition.Y > r.Size.Y + DespawnMargin)
		{
			EmitSignal(SignalName.Despawned);
			QueueFree();
		}
	}

	// === Collision handlers (ปากปลาอยู่กลุ่ม "PlayerMouth") ===
	private void OnHitboxAreaEntered(Area2D a)
	{
		if (a.IsInGroup("PlayerMouth"))
			Consume();
	}

	private void OnHitboxBodyEntered(Node b)
	{
		if (b.IsInGroup("PlayerMouth"))
			Consume();
	}

	// === Public API: ให้ Player เรียกเมื่ออยาก “กินเหรียญ” ด้วยตัวเอง ===
	public void Consume()
	{
		EmitSignal(SignalName.Eaten, Value); // แจ้งคะแนนโบนัสให้สแปว์นเนอร์/SM
		QueueFree();
	}
}
