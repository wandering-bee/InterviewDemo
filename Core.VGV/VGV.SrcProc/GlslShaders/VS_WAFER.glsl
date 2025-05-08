// Vertex Shader : basic.vert
#version 460 core

layout(location = 0) in vec3 vCoordinate;
layout(location = 1) in vec3 vNormal;
layout(location = 2) in vec3 vColor;

uniform mat4 cModel;
uniform mat4 cView;
uniform mat4 cPMatrix;
uniform float zMultiplier;

out vec3 Color;
out vec3 Normal;
out vec3 FragPos;

void main()
{
    Color  = vColor;
    Normal = mat3(transpose(inverse(cModel))) * vNormal;

    vec4 coord = vec4(vCoordinate.x / 100000.0,
                      vCoordinate.y / 100000.0,
                      vCoordinate.z / 100000.0 * zMultiplier,
                      1.0);
    vec4 world = cModel * coord;
    FragPos    = world.xyz;

    gl_Position = cPMatrix * cView * world;
}
