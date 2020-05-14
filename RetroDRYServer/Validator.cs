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
        /// Validate the daton and populate Errors in this instance with the problems found.
        /// </summary>
        public async Task Validate(DatonDef datondef, Daton daton)
        {
            Errors = new List<string>();

            //built-in validation
            var r = RecurPoint.FromDaton(datondef, daton);
            if (r is RowRecurPoint rr) Validate(rr);
            else if (r is TableRecurPoint rt) Validate(rt);

            //custom validation
            if (datondef.CustomValidator != null)
            {
                var errs = await datondef.CustomValidator(daton);
                if (errs != null) Errors.AddRange(errs);
            }
        }

        private void Validate(RowRecurPoint rr)
        {
            foreach (var coldef in rr.TableDef.Cols)
            {
                if (coldef.IsComputed) continue;
                object value = rr.TableDef.RowType.GetField(coldef.Name).GetValue(rr.Row);
                string valueS = value == null ? "" : value.ToString();
                string prompt = DataDictionary.ResolvePrompt(coldef.Prompt, User, coldef.Name);

                //string length
                if (coldef.CSType == typeof(string))
                {
                    bool minOK = valueS.Length >= coldef.MinLength,
                        maxOK = coldef.MaxLength > 0 && valueS.Length <= coldef.MaxLength;
                    if (!minOK || !maxOK)
                        Errors.Add(string.Format(DataDictionary.ResolvePrompt(coldef.LengthValidationMessage, User,
                            defaultValue: "{0} must be {2} to {1} characters"), prompt, coldef.MaxLength, coldef.MinLength));
                }

                //numeric range
                if (Utils.IsSupportedNumericType(coldef.CSType) && coldef.MaxNumberValue != 0 && coldef.MinNumberValue != 0)
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

            foreach (var rt in rr.GetChildren())
                Validate(rt);
        }

        private void Validate(TableRecurPoint rt)
        {
            foreach (var rr in rt.GetRows())
                Validate(rr);
        }
    }
}
