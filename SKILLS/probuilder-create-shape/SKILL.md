---
name: probuilder-create-shape
description: "Creates a new ProBuilder mesh shape in the scene. ProBuilder shapes are editable 3D meshes
that can be modified using other ProBuilder tools like extrusion, beveling, etc."
---

# Create a ProBuilder shape

Creates a new ProBuilder mesh shape in the scene. ProBuilder shapes are editable 3D meshes
that can be modified using other ProBuilder tools like extrusion, beveling, etc.

## How to Call

### HTTP API (Direct Tool Execution)

Execute this tool directly via the MCP Plugin HTTP API:

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-create-shape \
  -H "Content-Type: application/json" \
  -d '{
  "shapeType": "string_value",
  "name": "string_value",
  "parentGameObjectRef": "string_value",
  "position": "string_value",
  "rotation": "string_value",
  "scale": "string_value",
  "size": "string_value",
  "isLocalSpace": false
}'
```

#### With Authorization (if required)

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-create-shape \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
  "shapeType": "string_value",
  "name": "string_value",
  "parentGameObjectRef": "string_value",
  "position": "string_value",
  "rotation": "string_value",
  "scale": "string_value",
  "size": "string_value",
  "isLocalSpace": false
}'
```

> The token is stored in the file: `UserSettings/AI-Game-Developer-Config.json`
> Using the format: `"token": "YOUR_TOKEN"`

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `shapeType` | `string` | Yes | The type of shape to create. |
| `name` | `string` | No | Name of the new GameObject. |
| `parentGameObjectRef` | `any` | No | Parent GameObject reference. If not provided, the shape will be created at the root of the scene. |
| `position` | `any` | No | Position of the shape in world or local space. |
| `rotation` | `any` | No | Rotation of the shape in euler angles (degrees). |
| `scale` | `any` | No | Scale of the shape. |
| `size` | `any` | No | Size of the shape (width, height, depth). Default is (1, 1, 1). |
| `isLocalSpace` | `boolean` | No | If true, position/rotation/scale are in local space relative to parent. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "shapeType": {
      "type": "string",
      "enum": [
        "Cube",
        "Stair",
        "CurvedStair",
        "Prism",
        "Cylinder",
        "Plane",
        "Door",
        "Pipe",
        "Cone",
        "Sprite",
        "Arch",
        "Sphere",
        "Torus"
      ],
      "description": "The type of shape to create."
    },
    "name": {
      "type": "string",
      "description": "Name of the new GameObject."
    },
    "parentGameObjectRef": {
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef",
      "description": "Parent GameObject reference. If not provided, the shape will be created at the root of the scene."
    },
    "position": {
      "$ref": "#/$defs/UnityEngine.Vector3",
      "description": "Position of the shape in world or local space."
    },
    "rotation": {
      "$ref": "#/$defs/UnityEngine.Vector3",
      "description": "Rotation of the shape in euler angles (degrees)."
    },
    "scale": {
      "$ref": "#/$defs/UnityEngine.Vector3",
      "description": "Scale of the shape."
    },
    "size": {
      "$ref": "#/$defs/UnityEngine.Vector3",
      "description": "Size of the shape (width, height, depth). Default is (1, 1, 1)."
    },
    "isLocalSpace": {
      "type": "boolean",
      "description": "If true, position/rotation/scale are in local space relative to parent."
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
    "shapeType"
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
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BCreateShapeResponse"
    }
  },
  "$defs": {
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BCreateShapeResponse": {
      "type": "object",
      "properties": {
        "gameObjectName": {
          "type": "string"
        },
        "instanceId": {
          "type": "integer"
        },
        "shapeType": {
          "type": "string"
        },
        "position": {
          "type": "string"
        },
        "rotation": {
          "type": "string"
        },
        "scale": {
          "type": "string"
        },
        "faceCount": {
          "type": "integer"
        },
        "vertexCount": {
          "type": "integer"
        },
        "edgeCount": {
          "type": "integer"
        }
      },
      "required": [
        "instanceId",
        "faceCount",
        "vertexCount",
        "edgeCount"
      ]
    }
  },
  "required": [
    "result"
  ]
}
```

