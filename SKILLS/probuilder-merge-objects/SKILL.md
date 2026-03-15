---
name: probuilder-merge-objects
description: "Combines multiple ProBuilder meshes into a single mesh.
Useful for optimizing draw calls or creating a unified object from parts.
The first mesh in the list becomes the target that others merge into.

Example: Merge a table made of separate leg and top meshes into one object."
---

# Merge multiple ProBuilder meshes into one

Combines multiple ProBuilder meshes into a single mesh.
Useful for optimizing draw calls or creating a unified object from parts.
The first mesh in the list becomes the target that others merge into.

Example: Merge a table made of separate leg and top meshes into one object.

## How to Call

### HTTP API (Direct Tool Execution)

Execute this tool directly via the MCP Plugin HTTP API:

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-merge-objects \
  -H "Content-Type: application/json" \
  -d '{
  "gameObjectRefs": "string_value",
  "deleteSourceObjects": false
}'
```

#### With Authorization (if required)

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-merge-objects \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
  "gameObjectRefs": "string_value",
  "deleteSourceObjects": false
}'
```

> The token is stored in the file: `UserSettings/AI-Game-Developer-Config.json`
> Using the format: `"token": "YOUR_TOKEN"`

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRefs` | `any` | Yes | Array of GameObject references with ProBuilderMesh components to merge. First object becomes the merge target. |
| `deleteSourceObjects` | `boolean` | No | If true, delete the source GameObjects after merging (except the target). Default is true. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRefs": {
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef[]",
      "description": "Array of GameObject references with ProBuilderMesh components to merge. First object becomes the merge target."
    },
    "deleteSourceObjects": {
      "type": "boolean",
      "description": "If true, delete the source GameObjects after merging (except the target). Default is true."
    }
  },
  "$defs": {
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
    "System.Type": {
      "type": "string"
    },
    "com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef[]": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef",
        "description": "Find GameObject in opened Prefab or in the active Scene."
      }
    }
  },
  "required": [
    "gameObjectRefs"
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
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BMergeObjectsResponse"
    }
  },
  "$defs": {
    "System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BSourceObjectInfo\u003E": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BSourceObjectInfo"
      }
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BSourceObjectInfo": {
      "type": "object",
      "properties": {
        "index": {
          "type": "integer"
        },
        "name": {
          "type": "string"
        },
        "status": {
          "type": "string"
        }
      },
      "required": [
        "index"
      ]
    },
    "System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BAdditionalMeshInfo\u003E": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BAdditionalMeshInfo"
      }
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BAdditionalMeshInfo": {
      "type": "object",
      "properties": {
        "name": {
          "type": "string"
        },
        "instanceId": {
          "type": "integer"
        }
      },
      "required": [
        "instanceId"
      ]
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BMergeObjectsResponse": {
      "type": "object",
      "properties": {
        "mergedMeshCount": {
          "type": "integer"
        },
        "resultMeshCount": {
          "type": "integer"
        },
        "targetObjectName": {
          "type": "string"
        },
        "targetInstanceId": {
          "type": "integer"
        },
        "objectsDeleted": {
          "type": "integer"
        },
        "totalFacesBefore": {
          "type": "integer"
        },
        "totalFacesAfter": {
          "type": "integer"
        },
        "totalVerticesBefore": {
          "type": "integer"
        },
        "totalVerticesAfter": {
          "type": "integer"
        },
        "sourceObjects": {
          "$ref": "#/$defs/System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BSourceObjectInfo\u003E"
        },
        "additionalMeshes": {
          "$ref": "#/$defs/System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BAdditionalMeshInfo\u003E"
        }
      },
      "required": [
        "mergedMeshCount",
        "resultMeshCount",
        "targetInstanceId",
        "objectsDeleted",
        "totalFacesBefore",
        "totalFacesAfter",
        "totalVerticesBefore",
        "totalVerticesAfter"
      ]
    }
  },
  "required": [
    "result"
  ]
}
```

