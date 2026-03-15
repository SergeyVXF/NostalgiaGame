---
name: probuilder-create-poly-shape
description: "Creates a 3D mesh from a 2D polygon outline. Perfect for:
- Floor plans and room layouts
- Custom terrain patches
- Architectural elements (walls, platforms)
- Any shape that can be defined by a 2D outline

The polygon is defined by an array of 2D points (x,z coordinates) that form the outline.
The shape is then extruded upward by the specified height.

Examples:
- Rectangle: points=[[0,0], [4,0], [4,3], [0,3]] height=2.5
- L-shape: points=[[0,0], [3,0], [3,2], [1,2], [1,3], [0,3]] height=3
- Triangle: points=[[0,0], [2,0], [1,1.7]] height=1"
---

# Create a ProBuilder shape from polygon points

Creates a 3D mesh from a 2D polygon outline. Perfect for:
- Floor plans and room layouts
- Custom terrain patches
- Architectural elements (walls, platforms)
- Any shape that can be defined by a 2D outline

The polygon is defined by an array of 2D points (x,z coordinates) that form the outline.
The shape is then extruded upward by the specified height.

Examples:
- Rectangle: points=[[0,0], [4,0], [4,3], [0,3]] height=2.5
- L-shape: points=[[0,0], [3,0], [3,2], [1,2], [1,3], [0,3]] height=3
- Triangle: points=[[0,0], [2,0], [1,1.7]] height=1

## How to Call

### HTTP API (Direct Tool Execution)

Execute this tool directly via the MCP Plugin HTTP API:

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-create-poly-shape \
  -H "Content-Type: application/json" \
  -d '{
  "points": "string_value",
  "height": 0,
  "name": "string_value",
  "parentGameObjectRef": "string_value",
  "position": "string_value",
  "rotation": "string_value",
  "flipNormals": false,
  "isLocalSpace": false
}'
```

#### With Authorization (if required)

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-create-poly-shape \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
  "points": "string_value",
  "height": 0,
  "name": "string_value",
  "parentGameObjectRef": "string_value",
  "position": "string_value",
  "rotation": "string_value",
  "flipNormals": false,
  "isLocalSpace": false
}'
```

> The token is stored in the file: `UserSettings/AI-Game-Developer-Config.json`
> Using the format: `"token": "YOUR_TOKEN"`

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `points` | `any` | Yes | 2D polygon points as [x,z] coordinates. Minimum 3 points. Points should be in clockwise or counter-clockwise order. Example: [[0,0], [4,0], [4,3], [0,3]] creates a 4x3 rectangle. |
| `height` | `number` | No | Height to extrude the polygon upward. Default is 1. |
| `name` | `string` | No | Name of the new GameObject. |
| `parentGameObjectRef` | `any` | No | Parent GameObject reference. If not provided, the shape will be created at the root of the scene. |
| `position` | `any` | No | Position of the shape in world or local space. |
| `rotation` | `any` | No | Rotation of the shape in euler angles (degrees). |
| `flipNormals` | `boolean` | No | If true, flip the normals so the faces point inward instead of outward. |
| `isLocalSpace` | `boolean` | No | If true, position/rotation are in local space relative to parent. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "points": {
      "$ref": "#/$defs/System.Single[][]",
      "description": "2D polygon points as [x,z] coordinates. Minimum 3 points. Points should be in clockwise or counter-clockwise order. Example: [[0,0], [4,0], [4,3], [0,3]] creates a 4x3 rectangle."
    },
    "height": {
      "type": "number",
      "description": "Height to extrude the polygon upward. Default is 1."
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
    "flipNormals": {
      "type": "boolean",
      "description": "If true, flip the normals so the faces point inward instead of outward."
    },
    "isLocalSpace": {
      "type": "boolean",
      "description": "If true, position/rotation are in local space relative to parent."
    }
  },
  "$defs": {
    "System.Single[]": {
      "type": "array",
      "items": {
        "type": "number"
      }
    },
    "System.Single[][]": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/System.Single[]"
      }
    },
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
    "points"
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
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BCreatePolyShapeResponse"
    }
  },
  "$defs": {
    "System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BPointInfo\u003E": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BPointInfo"
      }
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BPointInfo": {
      "type": "object",
      "properties": {
        "index": {
          "type": "integer"
        },
        "x": {
          "type": "number"
        },
        "z": {
          "type": "number"
        }
      },
      "required": [
        "index",
        "x",
        "z"
      ]
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BCreatePolyShapeResponse": {
      "type": "object",
      "properties": {
        "gameObjectName": {
          "type": "string"
        },
        "instanceId": {
          "type": "integer"
        },
        "position": {
          "type": "string"
        },
        "rotation": {
          "type": "string"
        },
        "pointCount": {
          "type": "integer"
        },
        "height": {
          "type": "number"
        },
        "flipNormals": {
          "type": "boolean"
        },
        "boundsSize": {
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
        },
        "inputPoints": {
          "$ref": "#/$defs/System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BPointInfo\u003E"
        }
      },
      "required": [
        "instanceId",
        "pointCount",
        "height",
        "flipNormals",
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

