using Godot;                                     // ใช้คลาสพื้นฐานจาก Godot เช่น Node, CharacterBody2D, Vector2

// คลาส Coin ใช้ควบคุมเหรียญในช่วงโบนัส (เหรียญตกจากฟ้าและให้คะแนนเมื่อผู้เล่นเก็บ)
public partial class Coin : CharacterBody2D      // สืบทอดจาก CharacterBody2D เพื่อใช้ระบบฟิสิกส์
{
	// กำหนดชนิดของเหรียญเป็น Enum เพื่อให้อ่านง่ายและหลีกเลี่ยงการพิมพ์ชื่อผิด
	public enum CoinType { Bronze, Silver, Gold } // มี 3 ประเภท: ทองแดง, เงิน, ทอง

	// ===== Signals =====
	[Signal] public delegate void EatenEventHandler(int value);  // ส่งสัญญาณเมื่อเหรียญถูกเก็บ (พร้อมส่งค่าคะแนน)
	[Signal] public delegate void DespawnedEventHandler();       // ส่งสัญญาณเมื่อเหรียญหายจากหน้าจอ (เช่น หลุดขอบล่าง)

	// ===== Export Variables =====
	[Export] public CoinType Type { get; set; } = CoinType.Bronze; // ชนิดของเหรียญ (กำหนดได้ใน Inspector)

	// ค่าคะแนนของเหรียญแต่ละประเภท (ใช้ตอนส่งสัญญาณ Eaten)
	[Export] public int BronzeValue { get; set; } = 100;  // ค่าคะแนนเหรียญทองแดง
	[Export] public int SilverValue { get; set; } = 250;  // ค่าคะแนนเหรียญเงิน
	[Export] public int GoldValue   { get; set; } = 500;  // ค่าคะแนนเหรียญทอง

	// การตั้งค่าการเคลื่อนไหวของเหรียญ
	[Export] public float FallSpeedMin   { get; set; } = 180f;  // ความเร็วตกต่ำสุด (สุ่ม)
	[Export] public float FallSpeedMax   { get; set; } = 280f;  // ความเร็วตกสูงสุด (สุ่ม)
	[Export] public float DriftXAmplitude{ get; set; } = 24f;   // ระยะการแกว่งซ้าย-ขวา
	[Export] public float DriftXSpeed    { get; set; } = 2.2f;  // ความเร็วในการแกว่ง (เรเดียนต่อวินาที)
	[Export] public float RotateSpeedRad { get; set; } = 0f;    // ความเร็วในการหมุน (ถ้าอยากให้เหรียญหมุน)
	[Export] public float DespawnMargin  { get; set; } = 64f;   // ระยะเผื่อขอบล่าง (เมื่อหลุดเกินนี้ให้ลบเหรียญออก)

	// ===== ตัวแปรภายใน (ไม่ให้แก้ใน Inspector) =====
	private float _fallSpeed;  // ความเร็วตกจริงของเหรียญ (สุ่มในแต่ละ instance)
	private float _t;          // ตัวแปรเวลาสำหรับคำนวณการแกว่ง sine
	private float _startX;     // ตำแหน่ง X ตอนเกิด (ใช้เป็นจุดกลางของการแกว่ง)

	private Area2D _hitbox;            // Node ลูกชื่อ "Hitbox" สำหรับตรวจชนกับ Player
	private AnimatedSprite2D _anim;    // Node ลูกชื่อ "AnimatedSprite2D" สำหรับเล่นอนิเมชัน

	// ===== Property: คืนค่าคะแนนของเหรียญตามชนิด =====
	public int Value => Type switch
	{
		CoinType.Bronze => BronzeValue, // ถ้าเป็นเหรียญทองแดง → 100
		CoinType.Silver => SilverValue, // ถ้าเป็นเหรียญเงิน → 250
		CoinType.Gold   => GoldValue,   // ถ้าเป็นเหรียญทอง → 500
		_ => 0
	};

	// เรียกครั้งแรกเมื่อ Node ถูกเพิ่มเข้า Scene Tree
	public override void _Ready()
	{
		// ดึง node ลูกชื่อ "Hitbox" เพื่อให้ตรวจชนกับ Player ได้
		_hitbox = GetNodeOrNull<Area2D>("Hitbox");
		if (_hitbox != null)
		{
			_hitbox.Monitoring  = true;   // เปิดให้ตรวจการชน
			_hitbox.Monitorable = true;   // ให้ node อื่นตรวจเราได้ด้วย
			_hitbox.AreaEntered += OnHitboxAreaEntered; // เมื่อชนกับ Area อื่น (เช่น collider ของ Player)
			_hitbox.BodyEntered += OnHitboxBodyEntered; // เมื่อชนกับ Body เช่นตัว Player
		}

		// ดึง node ลูกชื่อ "AnimatedSprite2D" เพื่อเล่นอนิเมชันของเหรียญ
		_anim = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (_anim != null)
		{
			// ตั้งชื่อแอนิเมชันตามชนิดเหรียญ เช่น "Bronze", "Silver", "Gold"
			var animName = Type.ToString();
			if (_anim.SpriteFrames != null && _anim.SpriteFrames.HasAnimation(animName))
				_anim.Play(animName); // ถ้ามีแอนิเมชันตรงชื่อ → เล่นแอนิเมชันนั้น
			else
				_anim.Play();         // ถ้าไม่มี → เล่นแอนิเมชันแรกสุดแทน
		}

		// สุ่มความเร็วตกของเหรียญระหว่างค่า Min-Max
		_fallSpeed = (float)GD.RandRange(FallSpeedMin, FallSpeedMax);

		// สุ่มค่าเริ่มต้นของเวลา sine เพื่อไม่ให้เหรียญทุกเหรียญแกว่งพร้อมกัน
		_t = (float)GD.RandRange(0.0, 100.0);

		// จำตำแหน่ง X เริ่มต้นไว้เป็นจุดอ้างอิงในการแกว่งซ้ายขวา
		_startX = GlobalPosition.X;
	}

	// ฟังก์ชันฟิสิกส์ เรียกทุก frame ที่มีการอัปเดตฟิสิกส์
	public override void _PhysicsProcess(double delta)
	{
		// ตั้งความเร็วให้เหรียญตกลงในแนวดิ่ง
		Velocity = new Vector2(0f, _fallSpeed);
		MoveAndSlide(); // สั่งให้เหรียญเคลื่อนตามฟิสิกส์ (จัดการแรงเสียดทาน/ชนเอง)

		// ทำให้เหรียญแกว่งซ้ายขวาแบบ sine wave
		_t += (float)delta * DriftXSpeed;  // เดินเวลาไปเรื่อย ๆ ตาม delta time
		float x = _startX + Mathf.Sin(_t) * DriftXAmplitude; // คำนวณตำแหน่ง X ใหม่
		GlobalPosition = new Vector2(x, GlobalPosition.Y);   // ตั้งตำแหน่งใหม่

		// ถ้ามีการตั้งให้หมุน → บวกองศาหมุนเพิ่มในแต่ละเฟรม
		if (RotateSpeedRad != 0f)
			Rotation += RotateSpeedRad * (float)delta;

		// ตรวจว่าหลุดขอบล่างของหน้าจอหรือยัง (ถ้าหลุดให้ลบออก)
		var r = GetViewportRect();
		if (GlobalPosition.Y > r.Size.Y + DespawnMargin)
		{
			EmitSignal(SignalName.Despawned); // แจ้งว่าเหรียญหายไป (ให้ BonusCoinSpawner จัดการลบ)
			QueueFree();                       // ลบ Node นี้ออกจาก Scene
		}
	}

	// เรียกเมื่อมี Area อื่นเข้าชน hitbox ของเหรียญ
	private void OnHitboxAreaEntered(Area2D a)
	{
		// ถ้า object ที่ชนอยู่ในกลุ่ม "PlayerMouth" (คือปากของปลา)
		if (a.IsInGroup("PlayerMouth"))
			Consume(); // เรียกฟังก์ชันกินเหรียญ
	}

	// เรียกเมื่อมี Body (เช่น Player) เข้ามาชน hitbox ของเหรียญ
	private void OnHitboxBodyEntered(Node b)
	{
		if (b.IsInGroup("PlayerMouth"))  // ตรวจว่าตัวที่เข้ามาชนอยู่ในกลุ่ม "PlayerMouth" (คือปากของผู้เล่น)
		Consume();                   // ถ้าใช่ → เรียกฟังก์ชัน Consume() เพื่อให้เหรียญถูกเก็บและส่งคะแนน
	}

	// ฟังก์ชันสาธารณะให้ Player เรียกได้เองเพื่อกินเหรียญ
	public void Consume()
	{
		EmitSignal(SignalName.Eaten, Value); // ส่งสัญญาณว่าเหรียญถูกเก็บ พร้อมค่าคะแนน
		QueueFree();                         // ลบเหรียญออกจากฉาก
	}
}
