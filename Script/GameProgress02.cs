using Godot;
using System;
using System.IO;
using System.Text.Json;

public partial class GameProgress02 : Node
{
	// ===== Singleton =====
	public static GameProgress02 Instance { get; private set; }

	// ===== Path per user =====
	private string SaveFolder => "user://SaveProgress/";
	private const string SaveExt = ".json";

	// ===== Data per user =====
	public class ProgressData
	{
		public string PlayerName { get; set; }
		public int CurrentLevel { get; set; } = 1;        // ด่านที่จะเริ่ม (resume)
		public int HighScore   { get; set; } = 0;        // สถิติรวม (เปลี่ยนเป็น TotalScore ได้)
		public int CurrentPlayingLevel { get; set; } = 1; // ด่านที่กำลังเล่น
		public int LastLevelScore      { get; set; } = 0; // คะแนนล่าสุดของด่านที่จบ
		public int HighestUnlockedLevel { get; set; } = 1;// ใช้แทน CurrentLevelIndex เดิม
		public string LastUpdated { get; set; }
	}

	// ===== mirror (ให้สคริปต์อื่นอ้างง่าย) =====
	public static int CurrentLevelIndex = 1;
	public static int CurrentPlayingLevel = 1;
	public static int LastLevelScore = 0;

	public override void _Ready()
	{
		Instance = this;
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(SaveFolder));
		// ถ้ามีผู้ใช้อยู่แล้วก็ซิงก์แบบเงียบ ๆ
		LoadCurrentUser();
	}

	// ---------- Utilities ----------
	private string PathFor(string playerName) =>
		ProjectSettings.GlobalizePath(SaveFolder + playerName + SaveExt);

	public bool ExistsForUser(string playerName) =>
		File.Exists(PathFor(playerName));

	private ProgressData Default(string name) => new ProgressData
	{
		PlayerName = name,
		CurrentLevel = 1,
		HighScore = 0,
		CurrentPlayingLevel = 1,
		LastLevelScore = 0,
		HighestUnlockedLevel = 1,
		LastUpdated = DateTime.UtcNow.ToString("o")
	};

	private void SyncMemoryFrom(ProgressData d)
	{
		if (d == null) return;
		CurrentLevelIndex   = Math.Max(1, d.HighestUnlockedLevel);
		CurrentPlayingLevel = Math.Max(1, d.CurrentPlayingLevel);
		LastLevelScore      = d.LastLevelScore;
	}

	private void SyncDataFromMemory(ProgressData d)
	{
		if (d == null) return;
		d.HighestUnlockedLevel = Math.Max(1, CurrentLevelIndex);
		d.CurrentPlayingLevel  = Math.Max(1, CurrentPlayingLevel);
		d.LastLevelScore       = LastLevelScore;
		if (d.CurrentLevel <= 0) d.CurrentLevel = 1;
	}

	// ---------- Load / Save ----------
	public ProgressData LoadForUser(string playerName)
	{
		if (string.IsNullOrEmpty(playerName))
		{
			GD.PushError("LoadForUser: empty playerName");
			return null;
		}

		var path = PathFor(playerName);
		if (!File.Exists(path)) return Default(playerName);

		try
		{
			var json = File.ReadAllText(path);
			return JsonSerializer.Deserialize<ProgressData>(json) ?? Default(playerName);
		}
		catch (Exception e)
		{
			GD.PushError($"Failed to load {playerName}: {e.Message}");
			return Default(playerName);
		}
	}

	public bool SaveForUser(ProgressData data)
	{
		if (data == null || string.IsNullOrEmpty(data.PlayerName)) return false;
		data.LastUpdated = DateTime.UtcNow.ToString("o");
		try
		{
			var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
			File.WriteAllText(PathFor(data.PlayerName), json);
			return true;
		}
		catch (Exception e)
		{
			GD.PushError($"Failed to save {data.PlayerName}: {e.Message}");
			return false;
		}
	}

	// ---------- API สำหรับผู้ใช้ปัจจุบัน ----------
	public ProgressData LoadCurrentUser()
	{
		var user = PlayerLogin.Instance?.CurrentUser;
		if (user == null) return null;

		var d = LoadForUser(user.PlayerName);
		SyncMemoryFrom(d);
		return d;
	}

	public bool SaveCurrentUser(ProgressData data)
	{
		var user = PlayerLogin.Instance?.CurrentUser;
		if (user == null || data == null) return false;
		data.PlayerName = user.PlayerName;
		SyncDataFromMemory(data);
		return SaveForUser(data);
	}

	// ---------- Compat (จาก GameProgress เดิม) ----------
	public static void Advance()
	{
		var user = PlayerLogin.Instance?.CurrentUser;
		if (user == null) return;

		var d = Instance.LoadForUser(user.PlayerName);
		Instance.SyncMemoryFrom(d);

		CurrentLevelIndex = Math.Max(CurrentLevelIndex + 1, d.HighestUnlockedLevel + 1);
		d.CurrentLevel = Math.Max(d.CurrentLevel, CurrentLevelIndex);

		Instance.SyncDataFromMemory(d);
		Instance.SaveForUser(d);
	}

	public static void Reset()
	{
		var user = PlayerLogin.Instance?.CurrentUser;
		if (user == null) return;

		var d = Instance.LoadForUser(user.PlayerName);
		CurrentLevelIndex   = 0;
		CurrentPlayingLevel = 1;
		LastLevelScore      = 0;

		Instance.SyncDataFromMemory(d);
		if (d.CurrentLevel <= 0) d.CurrentLevel = 1;
		Instance.SaveForUser(d);
	}

	public static void Save()
	{
		var user = PlayerLogin.Instance?.CurrentUser;
		if (user == null) return;

		var d = Instance.LoadForUser(user.PlayerName);
		Instance.SyncDataFromMemory(d);
		Instance.SaveForUser(d);
	}

	public static void Load()
	{
		var user = PlayerLogin.Instance?.CurrentUser;
		if (user == null) return;

		var d = Instance.LoadForUser(user.PlayerName);
		Instance.SyncMemoryFrom(d);
	}

	// ใช้ตอนจบด่าน
	public void SubmitLevelResult(int playedLevel, int levelScore, bool passed)
	{
		var user = PlayerLogin.Instance?.CurrentUser;
		if (user == null) return;

		var d = LoadForUser(user.PlayerName);
		d.CurrentPlayingLevel = playedLevel;
		d.LastLevelScore = levelScore;
		if (levelScore > d.HighScore) d.HighScore = levelScore;

		if (passed)
		{
			d.HighestUnlockedLevel = Math.Max(d.HighestUnlockedLevel, playedLevel + 1);
			d.CurrentLevel = Math.Max(d.CurrentLevel, d.HighestUnlockedLevel);
		}

		SyncMemoryFrom(d);
		SaveForUser(d);
	}
}
