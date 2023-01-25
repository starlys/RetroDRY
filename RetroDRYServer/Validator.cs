using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// Daton validator. 
    /// </summary>
    public class Validator
    {
        public List<string> Errors;
        private readonly IUser User;

        public Validator(IUser user)
        {
            User = user;
        }

        /// <summary>
        /// Validate the persiston and populate Errors in this instance with the problems found.
        /// </summary>
        public async Task ValidatePersiston(DatonDef datondef, Persiston daton)
        {
            Errors = new List<string>();

            //built-in validation
            var r = RecurPoint.FromDaton(datondef, daton);
            if (r is RowRecurPoint rr) Validate(rr);
            else if (r is TableRecurPoint rt) Validate(rt);

            //custom validation
            await daton.Validate(User, message => Errors.Add(message));
        }

        /// <summary>
        /// Validate the criteria of a requested viewon and populate Errors in this instance with the problems found.
        /// </summary>
        public async Task ValidateCriteria(DatonDef datondef, ViewonKey viewonKey)
        {
            Errors = new List<string>();

            //built-in validation
            if (viewonKey.Criteria != null) {
                foreach (var cri in viewonKey.Criteria)
                {
                    var coldef = datondef.CriteriaDef.FindCol(cri.Name);
                    if (coldef == null)
                        Errors.Add("Unknown parameter: " + cri.Name);
                    else
                        ValidateCol(coldef, cri.PackedValue, true);
                }
            }

            //custom validation
            var tempViewon0 = Utils.Construct(datondef.Type);
            if (tempViewon0 is Viewon tempViewon)
            {
                await tempViewon.ValidateCriteria(User, viewonKey, message => Errors.Add(message));
            }
        }

        private void Validate(RowRecurPoint rr)
        {
            foreach (var coldef in rr.TableDef.Cols)
            {
                if (coldef.IsComputedOrJoined) continue;
                object value = rr.Row.GetValue(coldef);
                ValidateCol(coldef, value, false);
            }

            foreach (var rt in rr.GetChildren())
                Validate(rt);
        }

        private void Validate(TableRecurPoint rt)
        {
            foreach (var rr in rt.GetRows())
                Validate(rr);
        }

        private void ValidateCol(ColDef coldef, object value, bool isCriterion)
        {
            string valueS = value == null ? "" : value.ToString();
            string prompt = DataDictionary.ResolvePrompt(coldef.Prompt, User, coldef.Name);

            //string length
            if (coldef.CSType == typeof(string))
            {
                bool minOK = valueS.Length >= coldef.MinLength,
                    maxOK = coldef.MaxLength == 0 || valueS.Length <= coldef.MaxLength;
                if (!minOK || !maxOK)
                    Errors.Add(string.Format(DataDictionary.ResolvePrompt(coldef.LengthValidationMessage, User,
                        defaultValue: "{0} must be {2} to {1} characters"), prompt, coldef.MaxLength, coldef.MinLength));
            }

            //numeric range
            if (!isCriterion && Utils.IsSupportedNumericType(coldef.CSType) && (coldef.MaxNumberValue != 0 || coldef.MinNumberValue != 0))
            {
                bool isOK;
                if (value == null)
                {
                    isOK = false;
                }
                else
                {
                    decimal d = Convert.ToDecimal(value);
                    bool minOK = d >= coldef.MinNumberValue,
                        maxOK = coldef.MaxNumberValue != 0 && d <= coldef.MaxNumberValue;
                    isOK = minOK && maxOK;
                }
                if (!isOK)
                    Errors.Add(string.Format(DataDictionary.ResolvePrompt(coldef.RangeValidationMessage, User,
                        defaultValue: "{0} must be in range {1}-{2}"), prompt, coldef.MinNumberValue, coldef.MaxNumberValue));
            }

            //regex
            if (coldef.CSType == typeof(string) && !string.IsNullOrEmpty(coldef.Regex))
            {
                if (!Regex.IsMatch(valueS, coldef.Regex))
                    Errors.Add(string.Format(DataDictionary.ResolvePrompt(coldef.RegexValidationMessage, User,
                        defaultValue: "{0} does not fit required pattern"), prompt));
            }
        }
    }
}
