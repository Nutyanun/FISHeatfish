using Godot;

public partial class LabelWithBg : Control
{
	[Export] public Label Label;
	[Export] public TextureRect Bg;
	[Export] public Vector2 Padding = new Vector2(12, 8); // ซ้ายขวา=12 บนล่าง=8

	public override void _Ready()
	{
		if (Label != null)
			Label.Resized += UpdateBg;
		UpdateBg();
	}

	private void UpdateBg()
	{
		if (Label == null || Bg == null) return;
		// จัด Label ไว้ที่ (0,0) ของกรุ๊ป แล้วให้ Bg ครอบด้วย padding
		Label.Position = Vector2.Zero;
		Bg.Position = -Padding;
		Bg.Size = Label.Size + Padding * 2f;
	}
}
