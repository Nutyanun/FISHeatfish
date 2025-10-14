using Godot;
using System;
using Game;

public partial class CrystalPickup : Area2D
{
	// ใช้ string สำหรับ Export (เลือกจาก dropdown ใน Inspector)
	[Export(PropertyHint.Enum, "Red,Blue,Green,Pink,Purple")]
	private string _typeName = "Blue";

	// ใช้ภายในเป็น enum (ไม่ export)
	public CrystalType Type { get; private set; } = CrystalType.Blue;

	[Export] public float Duration = 6f;

	// เปิดไว้: เดาสีจากชื่อโหนด/ซีนอัตโนมัติ
	[Export] public bool AutoDetectFromName = true;

	public override void _Ready()
	{
		// แปลง string → enum (จาก Inspector)
		try
		{
			Type = Enum.Parse<CrystalType>(_typeName, true);
		}
		catch
		{
			Type = CrystalType.Blue; // fallback
			GD.PushWarning($"[CrystalPickup] Unknown type name: {_typeName}, fallback to Blue");
		}

		if (AutoDetectFromName)
			TryAutoDetectType();
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
			GD.PushError("[CrystalPickup] No SkillManager found. Please add a node named 'SkillManager' with SkillManager.cs (under Player).");
			return;
		}

		sm.Apply(Type, Duration);

		// ลบตัวเองออกจากฉาก
		(GetParent() ?? this).QueueFree();
	}

	private void TryAutoDetectType()
	{
		StringName sn = GetParent()?.Name ?? Name;
		string n = sn.ToString().ToLowerInvariant();

		// ถ้า Type ตั้งเองแล้ว (ไม่ใช่ Blue เริ่มต้น) → ไม่ทับ
		if (Type != CrystalType.Blue)
			return;

		// ถ้าชื่อบอกว่า blue → ปล่อยไว้
		if (n.Contains("blue"))
			return;

		// เดาสีจากชื่อ
		if (n.Contains("red"))       Type = CrystalType.Red;
		else if (n.Contains("green")) Type = CrystalType.Green;
		else if (n.Contains("pink"))  Type = CrystalType.Pink;
		else if (n.Contains("purple"))Type = CrystalType.Purple;

		GD.Print($"[CrystalPickup] AutoDetect Type => {Type} (from name: {n})");
	}
}
