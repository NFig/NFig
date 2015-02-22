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

        public SettingInfo(string name, string description, IList<SettingValue<TTier, TDataCenter>> defaults, IList<SettingValue<TTier, TDataCenter>> overrides)
        {
            Name = name;
            Description = description;
            Defaults = defaults;
            Overrides = overrides;
        } 

        public SettingValue<TTier, TDataCenter> GetActiveDefault(TTier tier, TDataCenter dataCenter)
        {
            return GetActive(Defaults, tier, dataCenter);
        }

        public SettingValue<TTier, TDataCenter> GetActiveOverride(TTier tier, TDataCenter dataCenter)
        {
            return GetActive(Overrides, tier, dataCenter);
        }

        private SettingValue<TTier, TDataCenter> GetActive(IList<SettingValue<TTier, TDataCenter>> overrides, TTier tier, TDataCenter dataCenter)
        {
            SettingValue<TTier, TDataCenter> over = null;
            foreach (var o in overrides)
            {
                if (o.IsValidFor(tier, dataCenter) && o.IsMoreSpecificThan(over))
                    over = o;
            }

            return over;
        }
    }
}