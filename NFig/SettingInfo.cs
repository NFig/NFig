using System.Collections.Generic;

namespace NFig
{
    public class SettingInfo<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        public string Name { get; }
        public string Description { get; }
        public IList<SettingValue<TTier, TDataCenter>> Defaults { get; }
        public IList<SettingValue<TTier, TDataCenter>> Overrides { get; }

        internal SettingInfo(
            string name,
            string description,
            IList<SettingValue<TTier, TDataCenter>> defaults,
            IList<SettingValue<TTier, TDataCenter>> overrides
            )
        {
            Name = name;
            Description = description;
            Defaults = defaults;
            Overrides = overrides;
        } 

        public SettingValue<TTier, TDataCenter> GetActiveValueFor(TTier tier, TDataCenter dataCenter)
        {
            return GetOverrideFor(tier, dataCenter) ?? GetDefaultFor(tier, dataCenter);
        }

        public SettingValue<TTier, TDataCenter> GetDefaultFor(TTier tier, TDataCenter dataCenter)
        {
            return GetBestValueFor(Defaults, tier, dataCenter);
        }

        public SettingValue<TTier, TDataCenter> GetOverrideFor(TTier tier, TDataCenter dataCenter)
        {
            return GetBestValueFor(Overrides, tier, dataCenter);
        }

        internal static SettingValue<TTier, TDataCenter> GetBestValueFor(IList<SettingValue<TTier, TDataCenter>> values, TTier tier, TDataCenter dataCenter)
        {
            SettingValue<TTier, TDataCenter> best = null;
            foreach (var val in values)
            {
                if (val.IsValidFor(tier, dataCenter) && val.IsMoreSpecificThan(best))
                    best = val;
            }

            return best;
        }
    }
}
