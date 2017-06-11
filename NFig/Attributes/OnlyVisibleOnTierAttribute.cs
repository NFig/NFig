using System;

namespace NFig
{
    /// <summary>
    /// Allows you to mark certain enum values as only visible to a particular tier(s). The main use case is to annotate the DataCenter enum values since the
    /// list of data centers will likely vary per tier.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class OnlyVisibleOnTierAttribute : Attribute
    {
        /// <summary>
        /// The tier(s) where the value is visible.
        /// </summary>
        public object[] Tiers { get; }

        /// <summary>
        /// Allows you to mark certain enum values as only visible to a particular tier(s). The main use case is to annotate the DataCenter enum values since the
        /// list of data centers will likely vary per tier.
        /// </summary>
        /// <param name="tiers">
        /// One or more tiers. The type of each must be the same as the generic TTier argument used when instantiating
        /// <see cref="NFigStore{TTier,TDataCenter}"/>, otherwise they will be ignored.
        /// </param>
        public OnlyVisibleOnTierAttribute(params object[] tiers)
        {
            Tiers = tiers;
        }
    }
}