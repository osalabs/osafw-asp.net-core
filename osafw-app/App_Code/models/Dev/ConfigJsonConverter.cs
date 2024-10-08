// Custom json converter for development - ensure keys sorted in expected way in the output
// Important - pass ArrayList or Hashtable to trigger this converter
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024  Oleg Savchuk www.osalabs.com


using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace osafw;

public class ConfigJsonConverter : System.Text.Json.Serialization.JsonConverter<object>
{
    public readonly List<string> ordered_keys_entity = [
        "iname",
        "table",
        "name",
        "fw_name",
        "model_name",
        "is_fw",
        "db_config",
        "comments",
        "controller",
        "fields",
        "foreign_keys",
        "indexes",
        //controller 
        "title",
        "url",
        "is_lookup",
        "is_dynamic_show",
        "is_dynamic_showform",
        //field within fields
        "pos",
        "fw_type",
        "fw_subtype",
        "default",
        "maxlen",
        "is_nullable",
        "is_identity",
        "numeric_precision",
        "numeric_scale",
        "charset",
        "collation",
        "ui",
        // foreign_key within foreign_keys
        "column",
        "pk_table",
        "pk_column",
        // indexes
        "PK",
        "UX1",
        "IX1",
        "UX2",
        "IX2",
        "UX3",
        "IX3",
        "UX4",
        "IX4",
        "UX5",
        "IX5",
    ];

    //keys used in controller - we need them to be written in the specific order convenient to read by human developer
    public readonly List<string> ordered_keys_controller = [
        "model",
        "required_fields",
        "save_fields",
        "save_fields_checkboxes",
        "save_fields_nullable",
        "form_new_defaults",
        "search_fields",
        "list_sortdef",
        "list_sortmap",
        "related_field_name",
        "is_dynamic_index",
        "list_view",
        "view_list_defaults",
        "view_list_map",
        "view_list_custom",
        "is_dynamic_index_edit",
        "list_edit",
        "edit_list_defaults",
        "edit_list_map",
        "is_dynamic_show",
        "show_fields",
        "is_dynamic_showform",
        "showform_fields",
        //within show_fields/showform_fields single field
        "is_custom",
        "field",
        "type",
        "label",
        "model",
        "lookup_model",
        "lookup_table",
        "lookup_field",
        "lookup_tpl",
        "lookup_params",
        "is_inline",
        "class",
        "class_label",
        "class_contents",
        "class_control",
        "required",
        "validate",
        "maxlength",
        "min",
        "max",
        "is_option_empty",
        "option0_title",
        "conv"
    ];

    private List<string> ordered_keys;

    public ConfigJsonConverter()
    {
        // set default ordered_keys as controller+entity
        ordered_keys = new List<string>(ordered_keys_controller);
        ordered_keys.AddRange(ordered_keys_entity);
    }

    public void setOrderedKeys(IEnumerable<string> keys)
    {
        ordered_keys = new List<string>(keys);
    }

    //read is not needed, but methods need to implement
    public override Hashtable Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value is IDictionary<string, object> hashtable)
        {
            WriteDictionary(writer, hashtable, options);
        }
        else if (value is IList arrayList)
        {
            writer.WriteStartArray();
            foreach (var item in arrayList)
            {
                JsonSerializer.Serialize(writer, item, options);
            }
            writer.WriteEndArray();
        }
        else
        {
            // pass-through to standard serialization of other types
            JsonSerializer.Serialize(writer, value);
        }
    }
    public void WriteDictionary(Utf8JsonWriter writer, IDictionary<string, object> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        //remember written keys
        var hwritten = new Dictionary<string, bool>();

        //write specific keys first
        foreach (var key in ordered_keys)
        {
            if (value.ContainsKey(key))
            {
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, value[key], options);
                hwritten[key] = true;
            }
        }

        //then write rest of keys
        foreach (string key in value.Keys)
        {
            if (hwritten.ContainsKey(key))
                continue;
            writer.WritePropertyName(key);
            JsonSerializer.Serialize(writer, value[key], options);
        }

        writer.WriteEndObject();
    }
}