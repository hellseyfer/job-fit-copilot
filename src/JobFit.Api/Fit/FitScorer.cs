using JobFit.Api.Requirements;

namespace JobFit.Api.Fit;

public static class FitScorer
{
    public const int MustHaveWeight = 2;
    public const int NiceToHaveWeight = 1;

    // round(100 × Σ weight·value / Σ weight), with met 1, partial 0.5 and missing 0.
    public static int Score(IReadOnlyList<RequirementAssessment> assessments)
    {
        if (assessments.Count == 0)
        {
            return 0;
        }

        var total = assessments.Sum(a => Weight(a.Importance));
        var achieved = assessments.Sum(a => Weight(a.Importance) * Value(a.Status));
        return (int)Math.Round(100 * achieved / total, MidpointRounding.AwayFromZero);
    }

    private static double Weight(RequirementImportance importance) =>
        importance == RequirementImportance.MustHave ? MustHaveWeight : NiceToHaveWeight;

    private static double Value(FitStatus status) => status switch
    {
        FitStatus.Met => 1,
        FitStatus.Partial => 0.5,
        _ => 0,
    };
}
