using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Schedule4Net.Constraint
{
    public interface SingleItemConstraint
    {
        ConstraintDecision Check(ScheduledItem item);
    }
}
