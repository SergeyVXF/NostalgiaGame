---
name: probuilder-bridge
description: "Creates a new face connecting two edges.
Useful for connecting separate parts of geometry or filling gaps.

Example:
- edgeA=[0,1], edgeB=[4,5] creates a quad face between the two edges"
---

# Bridge two edges in a ProBuilder mesh

Creates a new face connecting two edges.
Useful for connecting separate parts of geometry or filling gaps.

Example:
- edgeA=[0,1], edgeB=[4,5] creates a quad face between the two edges

## How to Call

### HTTP API (Direct Tool Execution)

Execute this tool directly via the MCP Plugin HTTP API:

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-bridge \
  -H "Content-Type: application/json" \
  -d '{
  "gameObjectRef": "string_value",
  "edgeA": "string_value",
  "edgeB": "string_value",
  "allowNonManifold": false
}'
```

#### With Authorization (if required)

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-bridge \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
  "gameObjectRef": "string_value",
  "edgeA": "string_value",
  "edgeB": "string_value",
  "allowNonManifold": false
}'
```

> The token is stored in the file: `UserSettings/AI-Game-Developer-Config.json`
> Using the format: `"token": "YOUR_TOKEN"`

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | Reference to the GameObject with a ProBuilderMesh component. |
| `edgeA` | `any` | Yes | First edge as [vertexA, vertexB]. |
| `edgeB` | `any` | Yes | Second edge as [vertexA, vertexB]. |
| `allowNonManifold` | `boolean` | No | If true, allows creation of non-manifold geometry (edges shared by more than 2 faces). |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef",
      "description": "Reference to the GameObject with a ProBuilderMesh component."
    },
    "edgeA": {
      "$ref": "#/$defs/System.Int32[]",
      "description": "First edge as [vertexA, vertexB]."
    },
    "edgeB": {
      "$ref": "#/$defs/System.Int32[]",
      "description": "Second edge as [vertexA, vertexB]."
    },
    "allowNonManifold": {
      "type": "boolean",
      "description": "If true, allows creation of non-manifold geometry (edges shared by more than 2 faces)."
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
    "System.Int32[]": {
      "type": "array",
      "items": {
        "type": "integer"
      }
    }
  },
  "required": [
    "gameObjectRef",
    "edgeA",
    "edgeB"
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
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BBridgeResponse"
    }
  },
  "$defs": {
    "System.Int32[]": {
      "type": "array",
      "items": {
        "type": "integer"
      }
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BBridgeResponse": {
      "type": "object",
      "properties": {
        "edgeA": {
          "$ref": "#/$defs/System.Int32[]"
        },
        "edgeB": {
          "$ref": "#/$defs/System.Int32[]"
        },
        "newFaceIndex": {
          "type": "integer"
        },
        "allowNonManifold": {
          "type": "boolean"
        },
        "faceCountBefore": {
          "type": "integer"
        },
        "faceCountAfter": {
          "type": "integer"
        },
        "facesAdded": {
          "type": "integer"
        },
        "totalVertexCount": {
          "type": "integer"
        },
        "totalEdgeCount": {
          "type": "integer"
        }
      },
      "required": [
        "newFaceIndex",
        "allowNonManifold",
        "faceCountBefore",
        "faceCountAfter",
        "facesAdded",
        "totalVertexCount",
        "totalEdgeCount"
      ]
    }
  },
  "required": [
    "result"
  ]
}
```

