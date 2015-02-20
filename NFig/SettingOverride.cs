using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NFig
{
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "Yes, I know it's a static member in a generic type. The fields are also generic.")]
    public class SettingOverride<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        private static readonly EqualityComparer<TTier> s_tierComparer = EqualityComparer<TTier>.Default;
        private static readonly EqualityComparer<TDataCenter> s_dataCenterComparer = EqualityComparer<TDataCenter>.Default;

        public string Name { get; set; }
        public string Value { get; set; }
        public TTier? Tier { get; set; }
        public TDataCenter? DataCenter { get; set; }

        public bool IsValidFor(TTier tier, TDataCenter dataCenter)
        {
            if (Tier.HasValue && !s_tierComparer.Equals(Tier.Value, tier))
                return false;

            if (DataCenter.HasValue && !s_dataCenterComparer.Equals(DataCenter.Value, dataCenter))
                return false;

            return true;
        }

        public bool IsMoreSpecificThan(SettingOverride<TTier, TDataCenter> over)
        {
            if (over == null)
                return true;

            // tier is considered more important than dc, so this check is first
            if (Tier.HasValue != over.Tier.HasValue)
            {
                return Tier.HasValue;
            }

            if (DataCenter.HasValue != over.DataCenter.HasValue)
            {
                return DataCenter.HasValue;
            }

            return false;
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException",
            Justification = "The HasValue != HasValue checks eliminate the need to check both HasValue properties in the equality if statements.")]
        public bool HasSameTierAndDataCenter(SettingOverride<TTier, TDataCenter> over)
        {
            if (Tier.HasValue != over.Tier.HasValue)
                return false;

            if (Tier.HasValue && !s_tierComparer.Equals(Tier.Value, over.Tier.Value))
                return false;

            if (DataCenter.HasValue != over.DataCenter.HasValue)
                return false;

            if (DataCenter.HasValue && s_dataCenterComparer.Equals(DataCenter.Value, over.DataCenter.Value))
                return false;

            return true;
        }
    }
}
