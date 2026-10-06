namespace Kynakee.Modules.Projects.Domain.ValueObjects
{
    public enum ProjectPhase
    {
        Initialization = 0,
        Capture = 1,
        Context = 2,
        Scope = 3,
        Production = 4,
        Planning = 5,
        Valuation = 6,
        Review = 7,
        Offer = 8
    }

    public enum ProjectStatus
    {
        Active = 0,
        Paused = 1,
        Completed = 2,
        Cancelled = 3
    }

    public enum ProjectChannel
    {
        Web = 0,
        Telegram = 1,
        WhatsApp = 2
    }    

    public enum WorkItemAIStatus
    {
        GeneratedByAI = 0,
        ReviewedByHuman = 1,
        ModifiedByHuman = 2
    }

    public enum APUSource
    {
        Cached = 0,
        Revalued = 1,
        GeneratedNew = 2
    }

    public enum APUComponentType
    {
        Material = 0,
        Labor = 1,
        Equipment = 2,
        AuxiliaryMeans = 3,
        Subcontract = 4,
        Transport = 5
    }

    public enum OfferStatus
    {
        Draft = 0,
        Sent = 1,
        Accepted = 2,
        Rejected = 3,
        Expired = 4
    }

    public enum PrecedenceType
    {
        FinishToStart = 0,
        StartToStart = 1,
        FinishToFinish = 2,
        StartToFinish = 3
    }


    public enum PricingStatus
    {
        Pending = 0,
        Found = 1,
        Unavailable = 2
    }

    public enum PricingSource
    {        
        McpProvider = 1,
        CachedPrice = 2,
        AlternativeSource = 3,
        None = 0,

    }

    public enum TransportRateBasis
    {
        PerTrip,
        PerVehicleKilometer,
        PerTonKilometer,
        PerUnit
    }

}
