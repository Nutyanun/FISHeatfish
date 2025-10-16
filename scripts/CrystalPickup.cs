using Godot;
using System;

public partial class CrystalPickup : CharacterBody2D
{
	// ===== Config =====
	public enum CrystalType { Blue, Pink, Red, Green, Purple }
	public enum RedMode { Add, Sub }

	[Export] public CrystalType Type = CrystalType.Blue;
	[Export] public RedMode RedAction = RedMode.Add;

	[Export] public float PickupRadius = 40f; // ระยะจาก "ปาก" ที่จะถือว่าเก็บได้
	[Export] public float Duration = -1f;      // เวลาบัฟ
	[Export] public bool  Verbose = false;

	// ===== Refs =====
	private SkillManager _sm;
	private Player _player;
	private Node2D _mouthRef;

	// กันเก็บซ้ำ
	private bool _consumed = false;

	public override void _Ready()
	{
		// ให้ "รากคริสตัล" อยู่ในกลุ่ม Crystal (นับเป็น 1 ชิ้น)
		if (!IsInGroup("Crystal")) AddToGroup("Crystal");

		ResolveSkillManager();
		ResolvePlayerAndMouth();

		// ใช้โพลระยะในฟิสิกส์เฟรม (ไม่ต้องมี Area2D ลูก)
		SetPhysicsProcess(true);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_consumed) return;

		// รีโซลฟ์อ้างอิงซ้ำ เผื่อรีโหลดฉาก
		if (_player == null || _mouthRef == null) ResolvePlayerAndMouth();
		if (_sm == null) ResolveSkillManager();
		if (_player == null || _sm == null) return;

		// เก็บด้วย "ระยะจากปาก"
		if (_mouthRef != null &&
			GlobalPosition.DistanceTo(_mouthRef.GlobalPosition) <= PickupRadius)
		{
			if (Verbose) GD.Print("[CrystalPickup] Collected by distance.");
			CallDeferred(nameof(ApplyAndVanish)); // deferred กันคิวฟรีกลางเฟรม
		}
	}

	private void ApplyAndVanish()
	{
		if (_consumed) return;
		_consumed = true;                 // ✅ กันซ้ำ
		SetMeta("_picked", true);         // ✅ ให้ฝั่ง Player เช็คซ้ำได้

		if (_sm == null) ResolveSkillManager();
		if (_sm == null)
		{
			GD.PushWarning("[CrystalPickup] SkillManager not found → skip consume.");
			_consumed = false;
			return;
		}

		string id = Type switch
		{
			CrystalType.Blue   => "Blue",
			CrystalType.Pink   => "Pink",
			CrystalType.Red    => (RedAction == RedMode.Sub) ? "RedSub" : "RedAdd",
			CrystalType.Green  => "Green",
			CrystalType.Purple => "Purple",
			_ => ""
		};
		if (string.IsNullOrEmpty(id))
		{
			_consumed = false;
			return;
		}

		bool ok = _sm.Apply(id, Duration);
		if (!ok)
		{
			_consumed = false;
			SetMeta("_picked", false);
			return;
		}

		SetPhysicsProcess(false);
		QueueFree();
	}

	// ===== Helpers =====
	private void ResolveSkillManager()
	{
		_sm = GetNodeOrNull<SkillManager>("%SkillManager")
		   ?? GetTree().CurrentScene?.GetNodeOrNull<SkillManager>("SkillManager")
		   ?? GetTree().Root.GetNodeOrNull<SkillManager>("SkillManager");

		if (Verbose) GD.Print($"[CrystalPickup] SM resolved? {_sm != null}");
	}

	private void ResolvePlayerAndMouth()
	{
		_player = null;

		// หา Player จากกลุ่มก่อน
		foreach (var n in GetTree().GetNodesInGroup("Player"))
		{
			if (n is Player p) { _player = p; break; }
		}
		// ไม่เจอในกลุ่ม ลอง path ที่พบบ่อย
		_player ??= GetTree().CurrentScene?.GetNodeOrNull<Player>("Player")
				?? GetTree().Root.GetNodeOrNull<Player>("Player");

		// mouthRef = ปาก ถ้าไม่พบใช้ตัว Player เองแทน
		_mouthRef = _player as Node2D;
		if (_player != null)
		{
			var mouth = _player.GetNodeOrNull<Node2D>("MouthArea")
					?? _player.FindChild("MouthArea", true, false) as Node2D;
			if (mouth != null) _mouthRef = mouth;
		}

		if (Verbose) GD.Print($"[CrystalPickup] Player={_player!=null} MouthRef={_mouthRef!=null}");
	}

	// เผื่อฝั่งอื่นเรียกเก็บด้วยโค้ด
	public void CollectBy(Player who)
	{
		if (!_consumed) CallDeferred(nameof(ApplyAndVanish));
	}
	public void CollectBy(Node who)
	{
		if (!_consumed && who is Player) CallDeferred(nameof(ApplyAndVanish));
	}
}
