using System.Collections.Generic;

namespace Schedule4Net.Core
{
    public class ViolatorUpdate
    {
        public Violator UpdatedViolator { get; private set; }
        public IList<ViolationsManager.PartnerUpdate> PartnerUpdates { get; private set; }

        public ViolatorUpdate(Violator updatedViolator, IList<ViolationsManager.PartnerUpdate> partnerUpdates)
        {
            UpdatedViolator = updatedViolator;
            PartnerUpdates = partnerUpdates;
        }
    }
}
