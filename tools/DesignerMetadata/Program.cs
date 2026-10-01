using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UIFramework.Api;
using UIFramework.Core.Export;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;

namespace DesignerMetadata
{
    /// <summary>
    /// Writes the UI Framework Designer's metadata bundle (UIFrameworkDesigner/architecture.md §4) from the framework's
    /// own tables: <c>dotnet run --project tools/DesignerMetadata [-- outputDir]</c>. The output directory defaults to
    /// <c>UIFrameworkDesigner/src/generated</c>. The framework's game and SMAPI references are loaded from the game folder
    /// the build found.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string gamePath = Metadata("GamePath");
            AppDomain.CurrentDomain.AssemblyResolve += (_, e) => Resolve(gamePath, new AssemblyName(e.Name));
            string output = Path.GetFullPath(args.Length > 0 ? args[0] : Metadata("OutputDir"));
            Generate(output);
            return 0;
        }

        /// <summary>Kept out of <see cref="Main"/> so the framework is only loaded once the resolver is registered.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Generate(string output)
        {
            Directory.CreateDirectory(output);
            Write(output, "menu.schema.json", Json(SchemaWriter.MenuSchema()));
            Write(output, "element-types.json", Json(ElementTypesFile()));
            Write(output, "defaults.json", Json(DefaultsFile()));
            Write(output, "csharp-map.json", Json(CSharpMapFile()));
            Write(output, "framework-version.txt", FrameworkVersion());
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Files
        // ---------------------------------------------------------------------------------------------------------

        private static JObject ElementTypesFile()
        {
            return new JObject
            {
                ["common"] = new JArray(ElementTypes.Common),
                ["outKeys"] = new JArray(ElementTypes.OutKeys),
                ["types"] = new JArray(ElementTypes.All.Select(type =>
                {
                    IReadOnlyList<string>? leafStyle = ElementTypes.LeafStyleFields(type);
                    return new JObject
                    {
                        ["name"] = type,
                        ["members"] = new JArray(ElementTypes.Members(type)),
                        ["container"] = ElementTypes.IsContainer(type),
                        ["leafStyle"] = leafStyle == null ? JValue.CreateNull() : new JArray(leafStyle)
                    };
                }))
            };
        }

        /// <summary><see cref="DataDefaults"/>: its <c>Menu</c> class, then one class per element type in documentation order.</summary>
        private static JObject DefaultsFile()
        {
            Dictionary<string, Type> classes = typeof(DataDefaults).GetNestedTypes(BindingFlags.NonPublic).ToDictionary(t => t.Name, StringComparer.Ordinal);
            string[] unknown = classes.Keys.Where(name => name != nameof(DataDefaults.Menu) && ElementTypes.Canonical(name) != name).ToArray();
            if (unknown.Length > 0)
            {
                throw new InvalidOperationException($"DataDefaults has classes that are not element types: {string.Join(", ", unknown)}.");
            }

            var elements = new JObject();
            foreach (string type in ElementTypes.All.Where(classes.ContainsKey))
            {
                elements[type] = Constants(classes[type]);
            }

            return new JObject
            {
                ["menu"] = Constants(classes[nameof(DataDefaults.Menu)]),
                ["elements"] = elements
            };
        }

        /// <summary><see cref="CSharpEmitter.Calls"/> for the kinds that are data element types, with the API method's parameters.</summary>
        private static JObject CSharpMapFile()
        {
            MethodInfo[] api = new[] { typeof(IStardewUIApi) }.Concat(typeof(IStardewUIApi).GetInterfaces()).SelectMany(t => t.GetMethods()).ToArray();
            var nullability = new NullabilityInfoContext();
            var map = new JObject();
            foreach (string type in ElementTypes.All.Where(CSharpEmitter.Calls.ContainsKey))
            {
                CSharpEmitter.KindCall call = CSharpEmitter.Calls[type];
                MethodInfo[] overloads = api.Where(m => m.Name == call.Method).OrderByDescending(m => m.GetParameters().Length).ToArray();
                if (overloads.Length == 0)
                {
                    throw new InvalidOperationException($"IStardewUIApi has no method {call.Method} (the emitter's call for {type}).");
                }

                var entry = new JObject
                {
                    ["method"] = call.Method,
                    ["interface"] = call.Interface,
                    ["ctorArgs"] = new JArray(call.Consumed),
                    ["parameters"] = Parameters(overloads[0], nullability)
                };
                if (overloads.Length > 1)
                {
                    entry["overloads"] = new JArray(overloads.Skip(1).Select(m => Parameters(m, nullability)));
                }

                map[type] = entry;
            }

            return map;
        }

        /// <summary>The framework's version (the manifest's <c>%ProjectVersion%</c>), without build metadata.</summary>
        private static string FrameworkVersion()
        {
            string version = typeof(IStardewUIApi).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
            int plus = version.IndexOf('+');
            return (plus < 0 ? version : version[..plus]) + "\n";
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The constants of a <see cref="DataDefaults"/> class in declaration order, as the data format writes them.</summary>
        private static JObject Constants(Type type)
        {
            var result = new JObject();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic).Where(f => f.IsLiteral).OrderBy(f => f.MetadataToken))
            {
                result[field.Name] = field.GetValue(null) switch
                {
                    bool b => b ? "true" : "false",
                    IFormattable f and not Enum => f.ToString(null, CultureInfo.InvariantCulture),
                    object v => v.ToString(),
                    null => null
                };
            }

            return result;
        }

        private static JArray Parameters(MethodInfo method, NullabilityInfoContext nullability)
        {
            return new JArray(method.GetParameters().Select(p => new JObject
            {
                ["name"] = p.Name,
                ["type"] = TypeName(p.ParameterType, nullability.Create(p)),
                ["optional"] = p.IsOptional,
                ["default"] = p.HasDefaultValue ? Convert.ToString(p.DefaultValue, CultureInfo.InvariantCulture) : null
            }));
        }

        /// <summary>A parameter type as C# writes it (<c>Func&lt;string&gt;</c>, <c>Rectangle?</c>, <c>string[]</c>).</summary>
        private static string TypeName(Type type, NullabilityInfo? nullability)
        {
            Type? underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                return TypeName(underlying, null) + "?";
            }

            string name;
            if (type.IsArray)
            {
                name = TypeName(type.GetElementType()!, nullability?.ElementType) + "[]";
            }
            else if (type.IsGenericType)
            {
                Type[] args = type.GetGenericArguments();
                string generic = type.Name[..type.Name.IndexOf('`')];
                name = $"{generic}<{string.Join(", ", args.Select((a, i) => TypeName(a, nullability?.GenericTypeArguments.ElementAtOrDefault(i))))}>";
            }
            else
            {
                name = Type.GetTypeCode(type) switch
                {
                    TypeCode.Boolean => "bool",
                    TypeCode.Int32 => "int",
                    TypeCode.Int64 => "long",
                    TypeCode.Single => "float",
                    TypeCode.Double => "double",
                    TypeCode.String => "string",
                    _ => type == typeof(object) ? "object" : type.Name
                };
            }

            return !type.IsValueType && nullability?.ReadState == NullabilityState.Nullable ? name + "?" : name;
        }

        private static string Json(JToken token) => token.ToString(Formatting.Indented) + "\n";

        /// <summary>Write UTF-8 (no BOM) with LF line endings.</summary>
        private static void Write(string directory, string name, string text)
        {
            string path = Path.Combine(directory, name);
            File.WriteAllText(path, text.Replace("\r\n", "\n"));
            Console.WriteLine(path);
        }

        private static string Metadata(string key)
        {
            return typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value
                ?? throw new InvalidOperationException($"the build did not set {key}.");
        }

        /// <summary>The game, SMAPI and SMAPI-internal assemblies the framework references (not copied to the output).</summary>
        private static Assembly? Resolve(string gamePath, AssemblyName name)
        {
            foreach (string directory in new[] { gamePath, Path.Combine(gamePath, "smapi-internal") })
            {
                string path = Path.Combine(directory, name.Name + ".dll");
                if (File.Exists(path))
                {
                    return Assembly.LoadFrom(path);
                }
            }

            return null;
        }
    }
}
