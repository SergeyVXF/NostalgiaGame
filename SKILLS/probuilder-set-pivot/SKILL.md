---
name: probuilder-set-pivot
description: "Changes the pivot (origin) point of a ProBuilder mesh.
The mesh geometry is adjusted so the pivot moves without changing the visual position.

Examples:
- Center the pivot: pivotLocation=Center
- Set pivot to first vertex: pivotLocation=FirstVertex
- Set custom pivot: pivotLocation=Custom, customPosition=(0, 0, 0)"
---

# Set the pivot point of a ProBuilder mesh

Changes the pivot (origin) point of a ProBuilder mesh.
The mesh geometry is adjusted so the pivot moves without changing the visual position.

Examples:
- Center the pivot: pivotLocation=Center
- Set pivot to first vertex: pivotLocation=FirstVertex
- Set custom pivot: pivotLocation=Custom, customPosition=(0, 0, 0)

## How to Call

### HTTP API (Direct Tool Execution)

Execute this tool directly via the MCP Plugin HTTP API:

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-set-pivot \
  -H "Content-Type: application/json" \
  -d '{
  "gameObjectRef": "string_value",
  "pivotLocation": "string_value",
  "customPosition": "string_value"
}'
```

#### With Authorization (if required)

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-set-pivot \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
  "gameObjectRef": "string_value",
  "pivotLocation": "string_value",
  "customPosition": "string_value"
}'
```

> The token is stored in the file: `UserSettings/AI-Game-Developer-Config.json`
> Using the format: `"token": "YOUR_TOKEN"`

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | Reference to the GameObject with a ProBuilderMesh component. |
| `pivotLocation` | `string` | No | Where to place the pivot. |
| `customPosition` | `any` | No | Custom world position for pivot (only used when pivotLocation=Custom). |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef",
      "description": "Reference to the GameObject with a ProBuilderMesh component."
    },
    "pivotLocation": {
      "type": "string",
      "enum": [
        "Center",
        "FirstVertex",
        "Custom"
      ],
      "description": "Where to place the pivot."
    },
    "customPosition": {
      "$ref": "#/$defs/UnityEngine.Vector3",
      "description": "Custom world position for pivot (only used when pivotLocation=Custom)."
    }
  },
  "$defs": {
    "System.Type": {
      "type": "string"
    },
    "com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "type": "integer",
          "description": "instanceID of the UnityEngine.Object. If it is \u00270\u0027 and \u0027path\u0027, \u0027name\u0027, \u0027assetPath\u0027 and \u0027assetGuid\u0027 is not provided, empty or null, then it will be used as \u0027null\u0027. Priority: 1 (Recommended)"
        },
        "path": {
          "type": "string",
          "description": "Path of a GameObject in the hierarchy Sample \u0027character/hand/finger/particle\u0027. Priority: 2."
        },
        "name": {
          "type": "string",
          "description": "Name of a GameObject in hierarchy. Priority: 3."
        },
        "assetType": {
          "$ref": "#/$defs/System.Type",
          "description": "Type of the asset."
        },
        "assetPath": {
          "type": "string",
          "description": "Path to the asset within the project. Starts with \u0027Assets/\u0027"
        },
        "assetGuid": {
          "type": "string",
          "description": "Unique identifier for the asset."
        }
      },
      "required": [
        "instanceID"
      ],
      "description": "Find GameObject in opened Prefab or in the active Scene."
    },
    "UnityEngine.Vector3": {
      "type": "object",
      "properties": {
        "x": {
          "type": "number"
        },
        "y": {
          "type": "number"
        },
        "z": {
          "type": "number"
        }
      },
      "required": [
        "x",
        "y",
        "z"
      ],
      "additionalProperties": false
    }
  },
  "required": [
    "gameObjectRef"
  ]
}
```

## Output

### Output JSON Schema

```json
{
  "type": "object",
  "properties": {
    "result": {
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BSetPivotResponse"
    }
  },
  "$defs": {
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BSetPivotResponse": {
      "type": "object",
      "properties": {
        "pivotLocation": {
          "type": "string"
        },
        "oldPivot": {
          "type": "string"
        },
        "newPivot": {
          "type": "string"
        },
        "offsetApplied": {
          "type": "string"
        },
        "gameObjectName": {
          "type": "string"
        },
        "newPosition": {
          "type": "string"
        }
      }
    }
  },
  "required": [
    "result"
  ]
}
```

