namespace SampleEqSim.Core.Gem;

/// <summary>
/// プロセスプログラム (レシピ) の定義 (SEMI E30 Stream 7)
/// </summary>
public class ProcessProgram
{
    public string PpId   { get; }
    public byte[] Body   { get; set; }
    public DateTime LastModified { get; set; }

    public ProcessProgram(string ppId, byte[] body)
    {
        PpId         = ppId;
        Body         = body;
        LastModified = DateTime.Now;
    }

    /// Body を UTF-8 文字列として返す (テキスト形式レシピ向け)
    public string BodyText => System.Text.Encoding.UTF8.GetString(Body);
}
