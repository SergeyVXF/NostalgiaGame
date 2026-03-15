---
name: probuilder-delete-faces
description: "Deletes selected faces from a ProBuilder mesh.
You can select faces by index OR by direction (semantic selection).
Deleting faces creates holes in the mesh or removes geometry entirely.

Examples:
- Delete bottom face: faceDirection=\"down\"
- Delete specific faces: faceIndices=[0, 2, 4]"
---

# Delete ProBuilder faces

Deletes selected faces from a ProBuilder mesh.
You can select faces by index OR by direction (semantic selection).
Deleting faces creates holes in the mesh or removes geometry entirely.

Examples:
- Delete bottom face: faceDirection="down"
- Delete specific faces: faceIndices=[0, 2, 4]

## How to Call

### HTTP API (Direct Tool Execution)

Execute this tool directly via the MCP Plugin HTTP API:

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-delete-faces \
  -H "Content-Type: application/json" \
  -d '{
  "gameObjectRef": "string_value",
  "faceIndices": "string_value",
  "faceDirection": "string_value"
}'
```

#### With Authorization (if required)

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-delete-faces \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
  "gameObjectRef": "string_value",
  "faceIndices": "string_value",
  "faceDirection": "string_value"
}'
```

> The token is stored in the file: `UserSettings/AI-Game-Developer-Config.json`
> Using the format: `"token": "YOUR_TOKEN"`

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | Reference to the GameObject with a ProBuilderMesh component. |
| `faceIndices` | `any` | No | Array of face indices to delete. Use this OR faceDirection, not both. Use ProBuilder_GetMeshInfo to get valid face indices. |
| `faceDirection` | `any` | No | Semantic face selection by direction. Use this OR faceIndices, not both. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef",
      "description": "Reference to the GameObject with a ProBuilderMesh component."
    },
    "faceIndices": {
      "$ref": "#/$defs/System.Int32[]",
      "description": "Array of face indices to delete. Use this OR faceDirection, not both. Use ProBuilder_GetMeshInfo to get valid face indices."
    },
    "faceDirection": {
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.FaceDirection",
      "description": "Semantic face selection by direction. Use this OR faceIndices, not both."
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
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.FaceDirection": {
      "type": "string",
      "enum": [
        "Up",
        "Down",
        "Left",
        "Right",
        "Forward",
        "Back"
      ]
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
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BDeleteFacesResponse"
    }
  },
  "$defs": {
    "System.Int32[]": {
      "type": "array",
      "items": {
        "type": "integer"
      }
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BDeleteFacesResponse": {
      "type": "object",
      "properties": {
        "deletedFaceCount": {
          "type": "integer"
        },
        "selectionMethod": {
          "type": "string"
        },
        "deletedFaceIndices": {
          "$ref": "#/$defs/System.Int32[]"
        },
        "facesRemoved": {
          "type": "integer"
        },
        "verticesRemoved": {
          "type": "integer"
        },
        "totalFaceCount": {
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
        "deletedFaceCount",
        "facesRemoved",
        "verticesRemoved",
        "totalFaceCount",
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

