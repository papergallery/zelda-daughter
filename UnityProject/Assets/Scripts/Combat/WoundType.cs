namespace ZeldaDaughter.Combat
{
    public enum WoundType
    {
        Puncture,   // колотая/резаная — кровотечение
        Fracture,   // перелом — замедление
        Burn,       // ожог — снижение точности
        Poison,     // отравление — тошнота, деградация
        None        // не назначен (используется в ItemData по умолчанию)
    }
}
