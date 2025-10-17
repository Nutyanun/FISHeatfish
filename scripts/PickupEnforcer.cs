using Godot;                               // ใช้คลาสจากเอนจิน Godot (Node, Input, ฯลฯ)
using System;                               // เนมสเปซมาตรฐานของ C#

public partial class PickupEnforcer : Node   // ตัวช่วย "ดูด/บังคับเก็บ" คริสตัลใกล้ปากผู้เล่น
{
	[Export] public float Radius = 40f;                  // รัศมีดูดเก็บ (พิกเซล) วัดจากตำแหน่งปาก
	[Export] public bool AutoCollect = false;            // ค่าเริ่มต้น: ปิดออโต้เก็บ (ถ้า true จะเก็บอัตโนมัติทุกเฟรมเมื่อเข้าเขต)
	[Export] public bool CollectOnlyWhenBitePressed = true; // ถ้า true จะเก็บได้เฉพาะเฟรมที่เพิ่ง "กดกัด/กดยืนยัน"
	[Export] public bool Verbose = false;                // เปิด/ปิดข้อความดีบัก

	private Player _player;                              // อ้างอิงผู้เล่น
	private SkillManager _skm;                           // อ้างอิงตัวจัดการสกิล/เอฟเฟกต์
	private Node2D _mouthRef;                            // อ้างอิงตำแหน่ง "ปาก" ของผู้เล่น (หรือผู้เล่นเองถ้าไม่พบ)
	private float _cooldown;                             // กันเก็บซ้ำรัว ๆ (ตอนนี้ปิดการใช้งาน แต่เก็บตัวแปรไว้)

	public override void _Ready()                        // เรียกครั้งเดียวเมื่อโหนดพร้อมทำงาน
	{
	ProcessMode = ProcessModeEnum.Always;           // ให้ _Process ทำงานแม้เกมจะ pause (เผื่ออยากเก็บตอน pause)
	ResolveRefs();                                  // หาและผูกอ้างอิง Player, Mouth, SkillManager
	}

	public override void _Process(double delta)         // เรียกทุกเฟรม (ไม่ใช่ฟิสิกส์)
	{
	// ถ้าอยากให้ต้องกดกัดค่อยเก็บ ก็ปล่อยบรรทัดนี้ไว้
	if (CollectOnlyWhenBitePressed && !Input.IsActionJustPressed("bite") && !Input.IsActionJustPressed("ui_accept"))
	return;                                         // ถ้าตั้งโหมด "ต้องกด" แต่เฟรมนี้ไม่ได้เพิ่งกด bite/ui_accept → ออกเลย

	if (_player == null || _skm == null) { ResolveRefs(); if (_player == null) return; } // ถ้าอ้างอิงหาย ให้ลองหาใหม่; ยังไม่เจอ player ก็ออก

	Vector2 mouthPos = (_mouthRef != null) ? _mouthRef.GlobalPosition : _player.GlobalPosition; // พิกัดปาก (fallback เป็นตัวผู้เล่น)

	int picked = 0;              // ← นับจำนวนที่เก็บได้ในเฟรมนี้
	const int PICK_LIMIT = 8;    // เผื่อกันลากทั้งจอในเฟรมเดียว (จำกัดจำนวนสูงสุดต่อเฟรม)

	foreach (var n in GetTree().GetNodesInGroup("Crystal")) // ไล่ทุกโหนดในกลุ่ม "Crystal" (เฉพาะรากคริสตัล)
	{
	if (n is not Node2D node || !IsInstanceValid(node)) continue; // ต้องเป็น Node2D และยัง valid
	if (node.GlobalPosition.DistanceTo(mouthPos) > Radius) continue; // อยู่นอกระยะดูด → ข้าม

	// ไม่ใช้คูลดาวน์แบบเดิมแล้ว
	// if (_cooldown > 0f) return;                                     // เดิมป้องกันเก็บซ้ำในเฟรมติดกัน

	var cp = node.GetNodeOrNull<CrystalPickup>(".") ?? node.GetChildOrNull<CrystalPickup>(true); // หาสคริปต์ CrystalPickup ที่รากหรือเป็นลูกลึก
	if (cp != null)
	{
	cp.CallDeferred("CollectBy", _player);                       // ถ้ามี CrystalPickup ให้เรียกเก็บผ่าน API ของมัน (deferred กันคิวฟรีกลางเฟรม)
	}
	else
	{
	string id = GuessCrystalId(node);                            // ถ้าไม่มีสคริปต์ ให้เดา id ของคริสตัลจาก meta/name
	if (string.IsNullOrEmpty(id)) id = "Pink";                   // เดาไม่ได้ → ดีฟอลต์เป็น Pink
	_skm.Apply(id, 8f);                                          // สั่ง SkillManager ใช้เอฟเฟกต์ (สมมุติ 8 วิ)
	SafeFreeCrystal(node);                                       // ลบคริสตัลออกจากซีนอย่างปลอดภัย (ลบราก)
	}

	picked++;                                                        // เก็บไปแล้ว 1 ชิ้น
	if (picked >= PICK_LIMIT) break;                                 // ถึงโควตาต่อเฟรมแล้ว → หยุด
	}

	// _cooldown = 0f; // ไม่ใช้แล้ว                                            // เดิมเคยรีเซ็ตคูลดาวน์ ที่นี่ปิดไป
	}

	// ===== Helpers =====

	private void ResolveRefs()                                           // พยายามหา Player, MouthArea, SkillManager ใหม่
	{
	foreach (var gn in GetTree().GetNodesInGroup("Player"))          // หา Player จาก group ก่อน
	if (gn is Player p) { _player = p; break; }                  // เจออันแรกก็พอ

	if (_player == null)                                            // ถ้าไม่เจอใน group
	_player = GetTree().CurrentScene?.GetNodeOrNull<Player>("Player") // ลองหาในซีนปัจจุบัน
	?? GetTree().Root.GetNodeOrNull<Player>("Player");       // หรือหาใต้ Root

	_mouthRef = null;                                               // รีเซ็ต mouthRef
	if (_player != null)
	{
	var mouth = _player.GetNodeOrNull<Node>("MouthArea")         // หาลูกชื่อ MouthArea โดยตรง
	 ?? _player.FindChild("MouthArea", true, false);    // หรือค้นแบบ recursive
	_mouthRef = mouth as Node2D ?? _player as Node2D;            // ถ้า mouth เป็น Node2D ใช้อันนั้น ไม่งั้นใช้ตัวผู้เล่น
	}

	_skm = GetNodeOrNull<SkillManager>("%SkillManager")              // หาด้วยชื่อ unique (%SkillManager)
	?? GetTree().CurrentScene?.GetNodeOrNull<SkillManager>("SkillManager") // หรือชื่อธรรมดาในซีนปัจจุบัน
	?? GetTree().Root.GetNodeOrNull<SkillManager>("SkillManager");         // หรือใต้ Root
	}

	private string GuessCrystalId(Node n)                                // เดา "รหัสสกิล" จากเมตา/ชื่อโหนด
	{
	if (n.HasMeta("crystal_id"))    return n.GetMeta("crystal_id").ToString(); // ถ้ามี meta ชื่อ crystal_id ใช้อันนั้น
	if (n.HasMeta("crystal_color")) return n.GetMeta("crystal_color").ToString(); // ถ้ามี crystal_color ก็ใช้เลย

	var name = n.Name.ToString().ToLowerInvariant();                 // เอาชื่อโหนดมาแปลงเป็นตัวพิมพ์เล็ก
	if (name.Contains("blue"))   return "Blue";                      // มีคำว่า blue → Blue
	if (name.Contains("pink"))   return "Pink";                      // pink → Pink
	if (name.Contains("purple")) return "Purple";                    // purple → Purple
	if (name.Contains("green"))  return "Green";                     // green → Green
	if (name.Contains("redadd") || name.Contains("red_plus") || name.Contains("red+") || name.Contains("time+") || name.Contains("addtime")) return "RedAdd"; // red add/time+
	if (name.Contains("redsub") || name.Contains("red_minus") || name.Contains("red-") || name.Contains("time-") || name.Contains("subtime")) return "RedSub"; // red sub/time-
	if (name.Contains("red")) return "RedAdd";                        // เจอ red แบบกลาง ๆ → เดาเป็น RedAdd
	return "";                                                       // เดาไม่ได้ → ค่่าว่าง (ให้ผู้เรียกจัดการต่อ)
	}

	private void SafeFreeCrystal(Node2D node)                            // ลบคริสตัลโดยพยายามลบรากที่อยู่ในกลุ่ม "Crystal"
	{
	Node root = node;                                                // เริ่มจากตัวที่รับมา
	while (root != null && !root.IsInGroup("Crystal")) root = root.GetParent(); // ไต่ขึ้นหาพ่อแม่จนเจอกลุ่ม "Crystal"
	(root ?? node).QueueFree();                                      // ถ้าเจอราก Crystal ให้ลบราก ไม่เจอให้ลบตัวเดิม
	}
	}

	public static class NodeExtensions                                     // คลาสขยายความสามารถของ Node (เมธอดช่วยค้นลูก)
	{
	public static T GetChildOrNull<T>(this Node parent, bool recursive = false) where T : class // หาลูกชนิด T ถ้าไม่เจอคืน null (เลือกค้นลึกได้)
	{
	if (parent == null) return null;                                 // ถ้าพ่อแม่เป็น null → ไม่มีให้หา
	foreach (Node c in parent.GetChildren())                         // ไล่ลูกโดยตรงทั้งหมด
	{
	if (c is T tc) return tc;                                    // ถ้าลูกเป็นชนิด T → คืนเลย
	if (recursive)                                               // ถ้าอนุญาตค้นลึก
	{
	var deep = c.GetChildOrNull<T>(true);                    // เรียกซ้ำลงไปชั้นถัดไป
	if (deep != null) return deep;                           // ถ้าเจอในชั้นลึก → คืนเลย
	}
	}
	return null;                                                     // ไล่ครบแล้วไม่เจอ → คืน null
	}
}
