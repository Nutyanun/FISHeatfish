using Godot;
using System.Linq;

public partial class CheckpointManager : Node2D
{
	public override void _Ready()
	{
		GameProgress.Load();   // โหลดความคืบหน้า
		UpdateLevelVisual();
	}

	private void UpdateLevelVisual()
{
	var Levels = GetChildren()
		.OfType<Level>()
		.OrderBy(m => ExtractNumber(m.Name.ToString()))
		.ToList();

	for (int idx = 0; idx < Levels.Count; idx++)
	{
		var script = Levels[idx];

		if (idx < GameProgress.CurrentLevelIndex)
		{
			// ผ่านแล้ว
			script.SetDone(true);
			script.SetActive(false);
			script.SelfModulate = new Color(1f, 0.4f, 0.4f);
		}
		else if (idx == GameProgress.CurrentLevelIndex)
		{
			// เลเวลล่าสุด (Active ตัวเดียวเท่านั้น)
			script.SetActive(true);
			script.SetDone(false);
			script.SelfModulate = new Color(1, 1, 1); // หรือให้วิบวับ
		}
		else
		{
			// ยังไม่ถึง
			script.SetDone(false);
			script.SetActive(false);
			script.SelfModulate = new Color(0.6f, 0.6f, 0.6f);
		}
	}
}

	private int ExtractNumber(string name)
	{
		string digits = new string(name.Where(char.IsDigit).ToArray());
		return string.IsNullOrEmpty(digits) ? 0 : int.Parse(digits);
	}
	
	public void Refresh()
	{
	GameProgress.Load();
	UpdateLevelVisual();
	}

}
