using Godot;
using System;

public partial class PickupEnforcer : Node
{
	[Export] public float Radius = 40f;                  // รัศมีดูดเก็บ
	[Export] public bool AutoCollect = false;            // ❗ค่าเริ่มต้น: ปิดออโต้เก็บ
	[Export] public bool CollectOnlyWhenBitePressed = true;
	[Export] public bool Verbose = false;

	private Player _player;
	private SkillManager _skm;
	private Node2D _mouthRef;
	private float _cooldown;                             // กันเก็บซ้ำรัว ๆ

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		ResolveRefs();
	}

	public override void _Process(double delta)
{
	// ถ้าอยากให้ต้องกดกัดค่อยเก็บ ก็ปล่อยบรรทัดนี้ไว้
	if (CollectOnlyWhenBitePressed && !Input.IsActionJustPressed("bite") && !Input.IsActionJustPressed("ui_accept"))
		return;

	if (_player == null || _skm == null) { ResolveRefs(); if (_player == null) return; }

	Vector2 mouthPos = (_mouthRef != null) ? _mouthRef.GlobalPosition : _player.GlobalPosition;

	int picked = 0;              // ← เก็บได้หลายชิ้น
	const int PICK_LIMIT = 8;    // เผื่อกันลากทั้งจอในเฟรมเดียว

	foreach (var n in GetTree().GetNodesInGroup("Crystal"))
	{
		if (n is not Node2D node || !IsInstanceValid(node)) continue;
		if (node.GlobalPosition.DistanceTo(mouthPos) > Radius) continue;

		// ไม่ใช้คูลดาวน์แบบเดิมแล้ว
		// if (_cooldown > 0f) return;

		var cp = node.GetNodeOrNull<CrystalPickup>(".") ?? node.GetChildOrNull<CrystalPickup>(true);
		if (cp != null)
		{
			cp.CallDeferred("CollectBy", _player);
		}
		else
		{
			string id = GuessCrystalId(node);
			if (string.IsNullOrEmpty(id)) id = "Pink";
			_skm.Apply(id, 8f);
			SafeFreeCrystal(node);
		}

		picked++;
		if (picked >= PICK_LIMIT) break;  // กันหนักเฟรมเดียว
	}

	// _cooldown = 0f; // ไม่ใช้แล้ว
}

	// ===== Helpers =====

	private void ResolveRefs()
	{
		foreach (var gn in GetTree().GetNodesInGroup("Player"))
			if (gn is Player p) { _player = p; break; }

		if (_player == null)
			_player = GetTree().CurrentScene?.GetNodeOrNull<Player>("Player")
					?? GetTree().Root.GetNodeOrNull<Player>("Player");

		_mouthRef = null;
		if (_player != null)
		{
			var mouth = _player.GetNodeOrNull<Node>("MouthArea")
					  ?? _player.FindChild("MouthArea", true, false);
			_mouthRef = mouth as Node2D ?? _player as Node2D;
		}

		_skm = GetNodeOrNull<SkillManager>("%SkillManager")
			?? GetTree().CurrentScene?.GetNodeOrNull<SkillManager>("SkillManager")
			?? GetTree().Root.GetNodeOrNull<SkillManager>("SkillManager");
	}

	private string GuessCrystalId(Node n)
	{
		if (n.HasMeta("crystal_id"))    return n.GetMeta("crystal_id").ToString();
		if (n.HasMeta("crystal_color")) return n.GetMeta("crystal_color").ToString();

		var name = n.Name.ToString().ToLowerInvariant();
		if (name.Contains("blue"))   return "Blue";
		if (name.Contains("pink"))   return "Pink";
		if (name.Contains("purple")) return "Purple";
		if (name.Contains("green"))  return "Green";
		if (name.Contains("redadd") || name.Contains("red_plus") || name.Contains("red+") || name.Contains("time+") || name.Contains("addtime")) return "RedAdd";
		if (name.Contains("redsub") || name.Contains("red_minus") || name.Contains("red-") || name.Contains("time-") || name.Contains("subtime")) return "RedSub";
		if (name.Contains("red")) return "RedAdd";
		return "";
	}

	private void SafeFreeCrystal(Node2D node)
	{
		Node root = node;
		while (root != null && !root.IsInGroup("Crystal")) root = root.GetParent();
		(root ?? node).QueueFree();
	}
}

public static class NodeExtensions
{
	public static T GetChildOrNull<T>(this Node parent, bool recursive = false) where T : class
	{
		if (parent == null) return null;
		foreach (Node c in parent.GetChildren())
		{
			if (c is T tc) return tc;
			if (recursive)
			{
				var deep = c.GetChildOrNull<T>(true);
				if (deep != null) return deep;
			}
		}
		return null;
	}
}
