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
        public TTier Tier { get; set; }
        public TDataCenter DataCenter { get; set; }

        public bool HasTier { get { return !s_tierComparer.Equals(Tier, default(TTier)); } }
        public bool HasDataCenter { get { return !s_dataCenterComparer.Equals(DataCenter, default(TDataCenter)); } }

        public bool IsValidFor(TTier tier, TDataCenter dataCenter)
        {
            if (HasTier && !s_tierComparer.Equals(Tier, tier))
                return false;

            if (HasDataCenter && !s_dataCenterComparer.Equals(DataCenter, dataCenter))
                return false;

            return true;
        }

        public bool IsMoreSpecificThan(SettingOverride<TTier, TDataCenter> over)
        {
            // tier is considered more important than dc, so this check is first
            if (HasTier != over.HasTier)
            {
                return HasTier;
            }

            if (HasDataCenter != over.HasDataCenter)
            {
                return HasDataCenter;
            }

            return false;
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException",
            Justification = "The HasValue != HasValue checks eliminate the need to check both HasValue properties in the equality if statements.")]
        public bool HasSameTierAndDataCenter(SettingOverride<TTier, TDataCenter> over)
        {
            return s_tierComparer.Equals(Tier, over.Tier) && s_dataCenterComparer.Equals(DataCenter, over.DataCenter);
        }
    }
}
