using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectDepthToPointCloud
{
    public class MarkerCondition
    {
        public enum Hand { Left, Right, Both, None };

        public Hand hand;
        public string marker;
        public string status;
        public bool fixation;

        public MarkerCondition(bool fixation) : this(Hand.None, "", "", fixation) { }

        public MarkerCondition(string status) : this(Hand.None, "", status, false) { }

        public MarkerCondition(Hand hand, string marker) : this(hand, marker, "", false) { }

        public MarkerCondition(Hand hand, string marker, string status, bool fixation)
        {
            this.hand = hand;
            this.marker = marker;
            this.status = status;
            this.fixation = fixation;
        }
    }
}
