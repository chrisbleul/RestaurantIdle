namespace BalancingCore.Tests;

public class ServiceTests
{
    [Fact]
    public void SofortBedient_GibtVollesTrinkgeld()
    {
        Assert.Equal(1.0 + Service.MaxTipBonus, Service.TipMultiplier(0.0), 6);
    }

    [Fact]
    public void ImLetztenMoment_GibtKeinTrinkgeld()
    {
        Assert.Equal(1.0, Service.TipMultiplier(1.0), 6);
    }

    [Theory]
    [InlineData(-5.0)]
    [InlineData(7.0)]
    public void AusserhalbDesBereichs_WirdGeklemmt(double waitFraction)
    {
        var value = Service.TipMultiplier(waitFraction);
        Assert.InRange(value, 1.0, 1.0 + Service.MaxTipBonus);
    }

    [Fact]
    public void TrinkgeldFaelltMonotonMitDerWartezeit()
    {
        Assert.True(Service.TipMultiplier(0.2) > Service.TipMultiplier(0.8));
    }
}

public class ReputationTests
{
    [Fact]
    public void StartwertIstNeutralerFlussMultiplikator()
    {
        Assert.Equal(1.0, Reputation.FlowMultiplier(Reputation.Start), 6);
    }

    [Fact]
    public void FlussMultiplikatorBleibtZwischenHalbUndAnderthalb()
    {
        Assert.Equal(0.5, Reputation.FlowMultiplier(Reputation.Min), 6);
        Assert.Equal(1.5, Reputation.FlowMultiplier(Reputation.Max), 6);
    }

    [Fact]
    public void BedienterGastHebtDenRuf_SchnellerServiceStaerker()
    {
        var langsam = Reputation.AfterServed(50.0, Service.TipMultiplier(1.0));
        var schnell = Reputation.AfterServed(50.0, Service.TipMultiplier(0.0));

        Assert.True(langsam > 50.0);
        Assert.True(schnell > langsam);
    }

    [Fact]
    public void VerlorenerGastKostetMehrAlsEinBedienterEinbringt()
    {
        var gewinn = Reputation.AfterServed(50.0, Service.TipMultiplier(0.0)) - 50.0;
        var verlust = 50.0 - Reputation.AfterLost(50.0);

        Assert.True(verlust > gewinn);
    }

    [Fact]
    public void AufgegebenesAnstehenKostetWenigerAlsEinIgnorierterPlatz()
    {
        Assert.True(Reputation.AfterQueueAbandoned(50.0) > Reputation.AfterLost(50.0));
        Assert.True(Reputation.AfterQueueAbandoned(50.0) < 50.0);
    }

    [Fact]
    public void RufVerlaesstDenGueltigenBereichNie()
    {
        Assert.Equal(Reputation.Max, Reputation.AfterServed(Reputation.Max, 1.5), 6);
        Assert.Equal(Reputation.Min, Reputation.AfterLost(Reputation.Min), 6);
        Assert.Equal(Reputation.Min, Reputation.AfterQueueAbandoned(Reputation.Min), 6);
    }
}
