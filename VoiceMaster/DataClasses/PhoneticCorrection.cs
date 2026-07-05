using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoiceMaster.DataClasses
{
    public class PhoneticCorrection : IComparable
    {
        public string OriginalText = "";
        public string CorrectedText = "";

        public PhoneticCorrection(string originalText,  string correctedText)
        {
            this.OriginalText = originalText;
            this.CorrectedText = correctedText;
        }

        public override string ToString() {
            return $"{OriginalText} - {CorrectedText}";
        }

        public override bool Equals(object? obj)
        {
            if (obj is not PhoneticCorrection other)
                return false;

            if (other.ToString().ToLower() == ToString().ToLower())
                return true;

            return false;
        }

        public override int GetHashCode()
        {
            return ToString().ToLowerInvariant().GetHashCode();
        }

        public int CompareTo(object? obj)
        {
            var otherObj = ((PhoneticCorrection)obj);
            return otherObj.ToString().ToLower().CompareTo(ToString().ToLower());
        }
    }
}
