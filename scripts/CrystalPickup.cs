using Godot;

public partial class CrystalPickup : Area2D
{
	[Export] public CrystalType Type = CrystalType.Blue;
	[Export] public float Duration = 6f;

	// เปิดไว้: เดาสีจากชื่อโหนด/ซีนอัตโนมัติถ้าลืมตั้ง Type ใน Inspector
	// จะ "ไม่ทับ" ค่า Type ที่ตั้งไว้ ยกเว้นกรณี Type ยังเป็น Blue แต่ชื่อบอกว่าเป็นสีอื่น
	[Export] public bool AutoDetectFromName = true;

	public override void _Ready()
	{
		if (AutoDetectFromName) TryAutoDetectType();
	}

	public void CollectBy(Player p)
	{
		if (p == null || !IsInstanceValid(p)) return;

		// หา SkillManager ใต้ Player ก่อน, ไม่มีก็หาในฉาก
		SkillManager sm =
			p.GetNodeOrNull<SkillManager>("SkillManager")
			?? (GetTree().CurrentScene?.FindChild("SkillManager", true, false) as SkillManager)
			?? (GetTree().Root?.FindChild("SkillManager", true, false) as SkillManager);

		if (sm == null)
		{
			GD.PushError("[CrystalPickup] No SkillManager found. Put a node named 'SkillManager' with SkillManager.cs (preferably under Player).");
			return;
		}

		sm.Apply(Type, Duration);

		// ลบทั้งชิ้น (สคริปต์มักติดที่ Hit ซึ่งเป็นลูกของราก)
		(GetParent() ?? this).QueueFree();
	}

	private void TryAutoDetectType()
{
	// Name เป็น StringName -> แปลงเป็น string ก่อนค่อย ToLowerInvariant()
	StringName sn = GetParent()?.Name ?? Name;
	string n = sn.ToString().ToLowerInvariant();

	// ถ้าตั้ง Type เองไว้แล้ว (ไม่ใช่ Blue ค่าเริ่ม) ไม่ทับ
	if (Type != CrystalType.Blue)
		return;

	// ถ้าชื่อบอกว่า blue อยู่แล้ว ก็ปล่อยไว้
	if (n.Contains("blue"))
		return;

	// เดาสีจากชื่อ
	if (n.Contains("red"))                 Type = CrystalType.Red;
	else if (n.Contains("green"))          Type = CrystalType.Green;
	else if (n.Contains("pink"))           Type = CrystalType.Pink;
	else if (n.Contains("purple"))         Type = CrystalType.Purple;

	GD.Print($"[CrystalPickup] AutoDetect Type => {Type} (from name: {n})");
}

}
