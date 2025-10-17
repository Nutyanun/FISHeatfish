using Godot;                            
using System;                           

// CrystalPickup: สคริปต์ที่ควบคุมการทำงานของ "คริสตัล" ที่ผู้เล่นสามารถเก็บได้
// ตรวจสอบระยะระหว่างปากปลาและคริสตัล แล้วเรียกใช้บัพผ่าน SkillManager
// หลังจากเก็บแล้วจะลบตัวเองออกจากฉาก
public partial class CrystalPickup : CharacterBody2D 
{
	// กำหนดชนิดของคริสตัลที่มีทั้งหมดในเกม
	public enum CrystalType { Blue, Pink, Red, Green, Purple }

	// กำหนดโหมดพิเศษของคริสตัลแดง ว่าจะเพิ่มเวลาหรือลดเวลา
	public enum RedMode { Add, Sub }

	// ตัวแปร Export สามารถตั้งค่าได้จาก Inspector ของ Godot
	[Export] public CrystalType Type = CrystalType.Blue;  // ชนิดของคริสตัล (เริ่มต้นเป็น Blue)
	[Export] public RedMode RedAction = RedMode.Add;      // โหมดของคริสตัลแดง (เริ่มต้นเป็น Add)

	[Export] public float PickupRadius = 40f;             // ระยะที่ผู้เล่นสามารถเก็บคริสตัลได้ (หน่วยพิกเซล)
	[Export] public float Duration = -1f;                 // ระยะเวลาที่บัพมีผล (-1 หมายถึงไม่มีหมดเวลา)
	[Export] public bool  Verbose = false;                // ถ้าเปิด true จะให้พิมพ์ log ใน Output เพื่อ debug

	// ตัวแปรอ้างอิงไปยัง Node อื่นในเกม
	private SkillManager _sm;                             // ตัวจัดการบัพทั้งหมดในเกม
	private Player _player;                               // ตัวผู้เล่นในฉาก
	private Node2D _mouthRef;                             // Node ปากของปลา ใช้ตรวจระยะเก็บ

	// ตัวแปรสถานะ ป้องกันไม่ให้เก็บคริสตัลซ้ำ
	private bool _consumed = false;

	// ฟังก์ชันนี้จะถูกเรียกอัตโนมัติเมื่อ Node ถูกเพิ่มเข้า Scene Tree
	public override void _Ready()
	{
		// ตรวจสอบว่าคริสตัลนี้อยู่ในกลุ่ม "Crystal" หรือยัง ถ้าไม่อยู่ให้เพิ่มเข้าไป
		if (!IsInGroup("Crystal")) AddToGroup("Crystal");

		// ค้นหา SkillManager และ Player ตอนเริ่มเกม
		ResolveSkillManager();
		ResolvePlayerAndMouth();

		// เปิดการอัปเดตฟิสิกส์ในทุกเฟรม เพื่อใช้ตรวจระยะการเก็บคริสตัล
		SetPhysicsProcess(true);
	}

	// ฟังก์ชันนี้จะถูกเรียกในทุกเฟรมฟิสิกส์ (ประมาณ 60 ครั้งต่อวินาที)
	public override void _PhysicsProcess(double delta)
	{
		// ถ้าคริสตัลถูกเก็บไปแล้ว ให้ออกจากฟังก์ชันทันที
		if (_consumed) return;

		// ตรวจสอบว่ามีการอ้างอิงถึง Player หรือ SkillManager อยู่หรือไม่
		// ถ้าไม่มีให้ลองค้นหาใหม่อีกครั้ง
		if (_player == null || _mouthRef == null) ResolvePlayerAndMouth();
		if (_sm == null) ResolveSkillManager();

		// ถ้ายังหาไม่เจอทั้ง Player หรือ SkillManager ให้หยุดทำงานในเฟรมนี้
		if (_player == null || _sm == null) return;

		// ตรวจระยะระหว่างตำแหน่งของปากปลาและคริสตัล
		// ถ้าอยู่ในระยะที่กำหนด (PickupRadius) ให้ถือว่าเก็บได้
		if (_mouthRef != null &&
			GlobalPosition.DistanceTo(_mouthRef.GlobalPosition) <= PickupRadius)
		{
			// ถ้าเปิดโหมด Verbose ให้แสดงข้อความใน Output สำหรับ debug
			if (Verbose) GD.Print("[CrystalPickup] Collected by distance.");

			// ใช้ CallDeferred เพื่อเลื่อนการเรียก ApplyAndVanish ไปหลังจบเฟรม
			// เพราะการลบ Node (QueueFree) กลางเฟรมอาจทำให้เกมเกิดปัญหา
			CallDeferred(nameof(ApplyAndVanish));
		}
	}

	// ฟังก์ชันนี้จะทำงานเมื่อคริสตัลถูกเก็บ
	// ใช้สำหรับเรียกบัพจาก SkillManager แล้วลบตัวเองออกจากฉาก
	private void ApplyAndVanish()
	{
		// ถ้าคริสตัลถูกเก็บไปแล้วให้ออกจากฟังก์ชันทันที
		if (_consumed) return;

		// ตั้งสถานะว่าคริสตัลนี้ถูกเก็บแล้ว
		_consumed = true;

		// บันทึกค่า metadata ว่า "_picked" เป็น true (ให้ node อื่นตรวจสอบได้)
		SetMeta("_picked", true);

		// ถ้ายังหา SkillManager ไม่เจอให้ลองค้นหาอีกครั้ง
		if (_sm == null) ResolveSkillManager();

		// ถ้ายังไม่เจอ SkillManager ให้เตือนใน Output แล้วไม่ทำอะไรต่อ
		if (_sm == null)
		{
			GD.PushWarning("[CrystalPickup] SkillManager not found → skip consume.");
			_consumed = false;
			return;
		}

		// แปลงชนิดคริสตัล (Enum) เป็นข้อความเพื่อส่งให้ SkillManager
		string id = Type switch
		{
			CrystalType.Blue   => "Blue",                 // คริสตัลสีน้ำเงิน: เพิ่มความเร็ว
			CrystalType.Pink   => "Pink",                 // คริสตัลสีชมพู: ดึงเหรียญ
			CrystalType.Red    => (RedAction == RedMode.Sub) ? "RedSub" : "RedAdd", // สีแดง: เพิ่มหรือลดเวลา
			CrystalType.Green  => "Green",                // สีเขียว: เกราะป้องกัน
			CrystalType.Purple => "Purple",               // สีม่วง: คูณคะแนน
			_ => ""                                       // ถ้าไม่มีชนิดที่ตรง → ไม่ทำอะไร
		};

		// ถ้า id ว่าง แปลว่าไม่รู้จักชนิดนี้ ให้ยกเลิกการเก็บ
		if (string.IsNullOrEmpty(id))
		{
			_consumed = false;
			return;
		}

		// เรียกใช้ฟังก์ชัน Apply ของ SkillManager เพื่อให้บัพทำงาน
		// ถ้า Apply คืนค่า false แสดงว่าบัพนี้ไม่สามารถใช้ได้
		bool ok = _sm.Apply(id, Duration);

		// ถ้าใช้บัพไม่สำเร็จ ให้ย้อนสถานะกลับ
		if (!ok)
		{
			_consumed = false;
			SetMeta("_picked", false);
			return;
		}

		// ปิดการอัปเดตฟิสิกส์ของ Node นี้ เพราะมันจะถูกลบออกแล้ว
		SetPhysicsProcess(false);

		// ลบ Node นี้ออกจาก Scene Tree
		QueueFree();
	}

	// ฟังก์ชันช่วยค้นหา SkillManager ใน Scene
	private void ResolveSkillManager()
	{
		// พยายามหาจาก NodePath ที่ชื่อ "%SkillManager" ก่อน
		// ถ้าไม่เจอให้หาจาก Scene ปัจจุบัน และสุดท้ายจาก Root
		_sm = GetNodeOrNull<SkillManager>("%SkillManager")
		   ?? GetTree().CurrentScene?.GetNodeOrNull<SkillManager>("SkillManager")
		   ?? GetTree().Root.GetNodeOrNull<SkillManager>("SkillManager");

		// ถ้าเปิด Verbose ให้แสดงผลว่าเจอหรือไม่
		if (Verbose) GD.Print($"[CrystalPickup] SM resolved? {_sm != null}");
	}

	// ฟังก์ชันช่วยค้นหา Player และ Node ปากปลา
	private void ResolvePlayerAndMouth()
	{
		// เริ่มต้นด้วยการรีเซ็ตตัวแปร Player
		_player = null;

		// พยายามหาผู้เล่นจากกลุ่มชื่อ "Player"
		foreach (var n in GetTree().GetNodesInGroup("Player"))
		{
			if (n is Player p) { _player = p; break; }
		}

		// ถ้าไม่พบในกลุ่ม ให้ลองหาจาก Scene ปัจจุบัน หรือ Root
		_player ??= GetTree().CurrentScene?.GetNodeOrNull<Player>("Player")
				?? GetTree().Root.GetNodeOrNull<Player>("Player");

		// ถ้ายังไม่พบ Node ปาก ให้ใช้ตัว Player เองแทน
		_mouthRef = _player as Node2D;

		if (_player != null)
		{
			// ค้นหา Node ที่ชื่อ "MouthArea" ซึ่งเป็นตำแหน่งปากปลา
			var mouth = _player.GetNodeOrNull<Node2D>("MouthArea")
					?? _player.FindChild("MouthArea", true, false) as Node2D;

			// ถ้าเจอ Node ปาก ให้เก็บอ้างอิงไว้
			if (mouth != null) _mouthRef = mouth;
		}

		// ถ้าเปิด Verbose ให้แสดงสถานะว่าเจอ Player และ Mouth หรือไม่
		if (Verbose) GD.Print($"[CrystalPickup] Player={_player!=null} MouthRef={_mouthRef!=null}");
	}

	// ฟังก์ชันให้ Node ภายนอก (เช่น Player) เรียกเพื่อเก็บคริสตัลด้วยโค้ด
	public void CollectBy(Player who)
	{
		// ถ้ายังไม่ได้เก็บ ให้เรียก ApplyAndVanish แบบ Deferred
		if (!_consumed) CallDeferred(nameof(ApplyAndVanish));
	}

	// ฟังก์ชัน Overload รองรับการเรียกด้วย Node ทั่วไป
	public void CollectBy(Node who)
	{
		// ถ้า Node ที่เรียกเป็น Player ให้เก็บได้
		if (!_consumed && who is Player) CallDeferred(nameof(ApplyAndVanish));
	}
}
