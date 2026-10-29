using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Unity.Hare.Content
{
    /// <summary>
    /// Fills a plain data class's public fields from a parsed JSON object (<see cref="MiniJson"/>'s tree), by
    /// name, case-insensitive, leaving a field at its existing (compiled-in) value when the data file doesn't
    /// mention it -- so a content file only needs to say what it changes from the baked default. Every DTO in
    /// <c>Unity.Hare.Content</c>/<c>Unity.Hare.Weapons</c>/<c>Unity.Hare.Player</c> that the registry loads is a
    /// plain class of public fields (floats, ints, bools, strings, enums, arrays of those, or a nested class of
    /// the same shape), so one reflective binder covers every section instead of one hand-written reader each.
    /// Every problem (wrong type, an unknown field, a bad enum name) is appended to <paramref name="errors"/>
    /// rather than thrown, so <see cref="ContentRegistry.FromJson"/> can report a whole file's problems at once.
    /// </summary>
    static class ContentBinder
    {
        public static void Populate(object target, object json, string path, List<string> errors)
        {
            if (json == null) return; // the section is absent: keep the compiled-in defaults
            if (!(json is Dictionary<string, object> obj))
            {
                errors.Add($"{path}: expected an object.");
                return;
            }

            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!obj.TryGetValue(field.Name, out object raw)) continue;
                matched.Add(field.Name);
                string fieldPath = path + "." + field.Name;
                if (TryConvert(raw, field.FieldType, field.GetValue(target), fieldPath, errors, out object converted))
                    field.SetValue(target, converted);
            }

            foreach (var key in obj.Keys)
                if (!matched.Contains(key))
                    errors.Add($"{path}.{key}: not a recognised field.");
        }

        static bool TryConvert(object raw, Type type, object existing, string path, List<string> errors, out object converted)
        {
            converted = null;

            if (raw == null)
            {
                errors.Add($"{path}: must not be null.");
                return false;
            }

            if (type == typeof(float) || type == typeof(double))
            {
                if (!(raw is double d) || double.IsNaN(d) || double.IsInfinity(d))
                {
                    errors.Add($"{path}: expected a finite number.");
                    return false;
                }
                if (type == typeof(float) && (d > float.MaxValue || d < float.MinValue))
                {
                    errors.Add($"{path}: {d} is too large for a 32-bit float.");
                    return false;
                }
                converted = type == typeof(float) ? (object)(float)d : d;
                return true;
            }

            if (type == typeof(int))
            {
                if (!(raw is double di) || double.IsNaN(di) || double.IsInfinity(di) || Math.Abs(di - Math.Round(di)) > 1e-6)
                {
                    errors.Add($"{path}: expected a whole number.");
                    return false;
                }
                if (di > int.MaxValue || di < int.MinValue)
                {
                    errors.Add($"{path}: {di} does not fit in a 32-bit integer.");
                    return false;
                }
                converted = (int)Math.Round(di);
                return true;
            }

            if (type == typeof(bool))
            {
                if (!(raw is bool b)) { errors.Add($"{path}: expected true or false."); return false; }
                converted = b;
                return true;
            }

            if (type == typeof(string))
            {
                if (!(raw is string s)) { errors.Add($"{path}: expected a string."); return false; }
                converted = s;
                return true;
            }

            if (type.IsEnum)
            {
                if (!(raw is string name) || !Enum.IsDefined(type, FixCase(type, name)))
                {
                    var names = string.Join(", ", Enum.GetNames(type));
                    errors.Add($"{path}: '{raw}' is not one of {names}.");
                    return false;
                }
                converted = Enum.Parse(type, FixCase(type, name));
                return true;
            }

            if (type.IsArray)
            {
                if (!(raw is List<object> items)) { errors.Add($"{path}: expected an array."); return false; }
                var elementType = type.GetElementType();
                var array = Array.CreateInstance(elementType, items.Count);
                bool ok = true;
                for (int i = 0; i < items.Count; i++)
                {
                    if (TryConvert(items[i], elementType, null, $"{path}[{i}]", errors, out object element))
                        array.SetValue(element, i);
                    else
                        ok = false;
                }
                converted = array;
                return ok;
            }

            if (type.IsClass)
            {
                // A nested plain-data class (e.g. AmmoTuning.GroundPrimary): populate the field's own instance
                // (creating one if it was null) so any field the file leaves out keeps that instance's default.
                object instance = existing ?? Activator.CreateInstance(type);
                Populate(instance, raw, path, errors);
                converted = instance;
                return true;
            }

            errors.Add($"{path}: don't know how to read a {type.Name}.");
            return false;
        }

        /// <summary>Case-insensitive enum lookup: finds the enum's own spelling for a differently-cased match.</summary>
        static string FixCase(Type enumType, string name)
        {
            foreach (var candidate in Enum.GetNames(enumType))
                if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            return name;
        }
    }
}
