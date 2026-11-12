using TMPro;
using UnityEngine;

// The cash balance on the vault timer's screen. It rolls up toward the real
// balance like a slot machine meter and punches on each win.
public class AlleyScoreDisplay : MonoBehaviour
{
    [SerializeField] AlleyChuteDirector director;
    [SerializeField] TextMeshPro readout;

    double shown = HeistSortRules.StartingBalance;
    float punch;
    Vector3 baseScale;

    public double Shown => shown;

    public void Configure(AlleyChuteDirector chute, TextMeshPro text)
    {
        director = chute;
        readout = text;
    }

    void Start()
    {
        baseScale = readout.transform.localScale;
        director.Rules.CashWon += (_, __) => punch = 1f;
        Write();
    }

    void Update()
    {
        double target = director.Rules.Balance;
        if (shown < target)
        {
            double rate = System.Math.Max(120d, (target - shown) * 5d);
            shown = System.Math.Min(target, shown + rate * Time.deltaTime);
        }
        else
        {
            shown = target;
        }
        punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 4f);
        readout.transform.localScale = baseScale * (1f + punch * 0.12f);
        Write();
    }

    void Write()
    {
        long value = (long)shown;
        readout.text = value < 100000 ? value.ToString("D5") : value.ToString();
    }
}
