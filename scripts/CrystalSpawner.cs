using Godot;
using System;
using System.Collections.Generic;

public partial class CrystalSpawner : Node2D
{
	[Export] public NodePath SpawnRootPath { get; set; } = "..";
	[Export] public float IntervalSec { get; set; } = 60f;
	[Export] public bool UseRandomInterval { get; set; } = false;
	[Export] public float RandomJitter { get; set; } = 0.25f;
	[Export] public int MaxOnScreen { get; set; } = 5;
	[Export] public float FallSpeed { get; set; } = 70f;
	[Export] public float MinVisibleSec { get; set; } = 2.5f;

	[Export] public bool SpawnImmediatelyOnStart { get; set; } = true;
	[Export] public bool LogDebug { get; set; } = true;

	[Export] public PackedScene PurpleScene { get; set; }
	[Export] public PackedScene BlueScene   { get; set; }
	[Export] public PackedScene GreenScene  { get; set; }
	[Export] public PackedScene RedScene    { get; set; }
	[Export] public PackedScene PinkScene   { get; set; }

	private Node2D _spawnRoot;
	private float _timer;
	private float _nextInterval;

	private readonly HashSet<CrystalType> _allowed = new();
	private readonly List<CrystalType> _allowedWithScene = new();
	private bool _finalPinkSpawned = false;

	public void ApplyRule(CrystalType[] colors, float intervalSec, int maxOnScreen)
	{
		_allowed.Clear();
		if (colors != null && colors.Length > 0)
			_allowed.UnionWith(colors);

		IntervalSec = Math.Max(0.1f, intervalSec);
		MaxOnScreen = Math.Max(0, maxOnScreen);

		RefreshAllowedWithScene();
		ResetPinkForced();

		_timer = 0f;
		_nextInterval = IntervalWithJitter();

		if (LogDebug) GD.Print($"[CrystalSpawner] ApplyRule OK colors={_allowedWithScene.Count} interval={IntervalSec}s max={MaxOnScreen}");
	}

	public void ResetPinkForced() => _finalPinkSpawned = false;

	public void ForcePinkOnceInLastWindow()
	{
		if (_finalPinkSpawned || PinkScene == null) return;
		if (!PinkExistsOnScreen())
			SpawnSpecific(PinkScene, force: true);
		_finalPinkSpawned = true;
		if (LogDebug) GD.Print("[CrystalSpawner] Force pink in last 20s.");
	}

	public override void _Ready()
	{
		_spawnRoot = GetNodeOrNull<Node2D>(SpawnRootPath) ?? GetParent() as Node2D ?? this;
		_timer = 0f;
		_nextInterval = IntervalWithJitter();
		
		if (SpawnImmediatelyOnStart && _allowedWithScene.Count > 0)
			TrySpawnOnceNow();
	}

	public override void _Process(double delta)
{
	if (!IsInsideTree()) return;
	if (_spawnRoot == null) return;
	if (!GodotObject.IsInstanceValid(_spawnRoot)) return;
	if (_spawnRoot.IsQueuedForDeletion() || !_spawnRoot.IsInsideTree()) return;
	// กันเคสซีน/รากโดนลบระหว่างเฟรม
	if (!IsInsideTree()) return;
	if (_spawnRoot == null || !GodotObject.IsInstanceValid(_spawnRoot) || !_spawnRoot.IsInsideTree())
		return;

	if (_allowedWithScene.Count == 0) return;

	_timer += (float)delta;
	if (_timer < _nextInterval) return;

	_timer -= _nextInterval;
	_nextInterval = IntervalWithJitter();

	int onScreen = CountCrystalsOnScreen();
	int room = (MaxOnScreen <= 0) ? 0 : Math.Max(0, MaxOnScreen - onScreen);
	if (room <= 0) return;                      // ← ไม่มีที่ว่าง/ปิดไว้ → ไม่สปอว์น
	int toSpawn = 1 + (int)(GD.Randi() % (uint)room);


	for (int i = 0; i < toSpawn; i++)
	{
		var color = PickAllowedColorWithScene();
		var scene = SceneOf(color);
		if (scene != null) SpawnSpecific(scene);
	}

	if (LogDebug) GD.Print($"[CrystalSpawner] Spawn batch={toSpawn} onScreen={CountCrystalsOnScreen()}");
}

	private void TrySpawnOnceNow()
	{
		if (_allowedWithScene.Count == 0) return;
		var color = PickAllowedColorWithScene();
		var scene = SceneOf(color);
		if (scene != null) { SpawnSpecific(scene, force: true); if (LogDebug) GD.Print("[CrystalSpawner] SpawnImmediatelyOnStart"); }
	}

	private float IntervalWithJitter()
	{
		if (!UseRandomInterval || RandomJitter <= 0f) return IntervalSec;
		float j = Mathf.Clamp(RandomJitter, 0f, 0.9f);
		float low = IntervalSec * (1f - j);
		float high = IntervalSec * (1f + j);
		return (float)GD.RandRange(low, high);
	}

	private void RefreshAllowedWithScene()
	{
		_allowedWithScene.Clear();
		foreach (var c in _allowed)
			if (SceneOf(c) != null)
				_allowedWithScene.Add(c);
	}

	private CrystalType PickAllowedColorWithScene()
	{
		int n = _allowedWithScene.Count;
		int idx = (int)(GD.Randi() % (uint)Math.Max(1, n));
		return (n == 0) ? CrystalType.Purple : _allowedWithScene[idx];
	}

	private PackedScene SceneOf(CrystalType c) => c switch
	{
		CrystalType.Purple => PurpleScene,
		CrystalType.Blue   => BlueScene,
		CrystalType.Green  => GreenScene,
		CrystalType.Red    => RedScene,
		CrystalType.Pink   => PinkScene,
		_ => null
	};

	private void SpawnSpecific(PackedScene ps, bool force = false)
	{
		if (ps == null || _spawnRoot == null) return;
		if (!GodotObject.IsInstanceValid(_spawnRoot) || _spawnRoot.IsQueuedForDeletion() || !_spawnRoot.IsInsideTree())
			return;
			
		if (ps == null || _spawnRoot == null) return;

		if (!force && MaxOnScreen > 0 && CountCrystalsOnScreen() >= MaxOnScreen)
			return;

		var n = ps.Instantiate<Node2D>();
		if (n == null) return;

		Rect2 vp = GetViewportRect();
		float x = (float)GD.RandRange(vp.Position.X + 32f, vp.End.X - 32f);
		float y = vp.Position.Y + 2f;
		n.GlobalPosition = new Vector2(x, y);

		n.AddToGroup("Crystal");                  // ✅ สำคัญ: เพื่อให้ Player สแกนเจอ
		if (ps == PinkScene) n.SetMeta("crystal_color", "pink");

		_spawnRoot.AddChild(n);

		StartFall(n);

		if (LogDebug) GD.Print($"[CrystalSpawner] +1 {ps.ResourcePath}");
	}

	private void StartFall(Node2D n)
	{
		if (n == null) return;
		FallDriver driver = new FallDriver(n, FallSpeed, MinVisibleSec);
		n.AddChild(driver);
	}

	private int CountCrystalsOnScreen()
{
	if (_spawnRoot == null ||
		!GodotObject.IsInstanceValid(_spawnRoot) ||
		_spawnRoot.IsQueuedForDeletion() ||
		!_spawnRoot.IsInsideTree())
		return 0;

	try
	{
		int count = 0;

		foreach (Node child in _spawnRoot.GetChildren())
		{
			if (child == null) continue;
			if (!GodotObject.IsInstanceValid(child)) continue;
			if (child.IsQueuedForDeletion() || !child.IsInsideTree()) continue;

			// นับเฉพาะ "รากคริสตัล" เท่านั้น
			if (!child.IsInGroup("Crystal")) continue;

			count++;
		}

		return count; // ✅ ต้องมี return ก่อนออกจาก try
	}
	catch (ObjectDisposedException)
	{
		return 0;     // ✅ ต้องมี catch/finally ปิด try
	}
}


	private bool PinkExistsOnScreen()
{
	if (_spawnRoot == null ||
		!GodotObject.IsInstanceValid(_spawnRoot) ||
		_spawnRoot.IsQueuedForDeletion() ||
		!_spawnRoot.IsInsideTree())
		return false;

	try
	{
		foreach (Node child in _spawnRoot.GetChildren())
		{
			if (child is not Node2D n2d) continue;
			if (!GodotObject.IsInstanceValid(n2d)) continue;
			if (n2d.IsQueuedForDeletion() || !n2d.IsInsideTree()) continue;

			Variant meta = n2d.GetMeta("crystal_color");
				string s = null;
			// Godot 4: ตรวจชนิดแล้วค่อยดึงออกมาเป็น string
			if (meta.VariantType == Variant.Type.String || meta.VariantType == Variant.Type.StringName)
				s = meta.AsString();

			if (s == "pink" && IsInsideViewport(n2d.GlobalPosition))
   				 return true;
 				
		}
	}
	catch (ObjectDisposedException)
	{
		return false;
	}
	return false;
}


	private bool IsInsideViewport(Vector2 g)
	{
		Rect2 vp = GetViewportRect();
		return g.X >= vp.Position.X && g.X <= vp.End.X && g.Y >= vp.Position.Y && g.Y <= vp.End.Y;
	}

	private List<CrystalType> AvailableColors()
	{
		var list = new List<CrystalType>(5);
		if (PurpleScene != null) list.Add(CrystalType.Purple);
		if (BlueScene   != null) list.Add(CrystalType.Blue);
		if (GreenScene  != null) list.Add(CrystalType.Green);
		if (RedScene    != null) list.Add(CrystalType.Red);
		if (PinkScene   != null) list.Add(CrystalType.Pink);
		return list;
	}

	private void EnsureAllowedFallback()
	{
		if (_allowedWithScene.Count > 0) return;
		var avail = AvailableColors();
		if (avail.Count > 0)
		{
			_allowed.Clear();
			foreach (var c in avail) _allowed.Add(c);
			_allowedWithScene.Clear();
			_allowedWithScene.AddRange(avail);
			GD.PushWarning("[CrystalSpawner] Allowed empty → fallback to all available PackedScenes.");
		}
	}

	private sealed partial class FallDriver : Node
	{
		private readonly Node2D _host;
		private readonly float _fallSpeed;
		private readonly float _minVisible;
		private float _t;
		private float _life;

		public FallDriver(Node2D host, float fallSpeed, float minVisible)
		{
			_host = host;
			_fallSpeed = Math.Max(20f, fallSpeed);
			_minVisible = Math.Max(0.2f, minVisible);
			ProcessMode = ProcessModeEnum.Always;
		}

		public override void _Process(double delta)
		{
			if (!IsInstanceValid(_host)) { QueueFree(); return; }

			_life += (float)delta;
			_t += (float)delta;
			float drift = Mathf.Sin(_t * 2.2f) * 18f;

			_host.GlobalPosition += new Vector2(drift, _fallSpeed) * (float)delta;

			var vp = _host.GetViewportRect();
			bool passedBottom = _host.GlobalPosition.Y > vp.End.Y + 80f;
			if ((_life >= _minVisible && passedBottom) || _life > 12f)
			{
				Node root = _host;
				while (root != null && !root.IsInGroup("Crystal")) root = root.GetParent();
				(root ?? (Node)_host).QueueFree();
				QueueFree();
			}
		}
	}
	public override void _ExitTree()
{
	// ซีน/รากกำลังถูกทำลาย: หยุด Process และตัด reference
	SetProcess(false);
	_spawnRoot = null;
}

}
