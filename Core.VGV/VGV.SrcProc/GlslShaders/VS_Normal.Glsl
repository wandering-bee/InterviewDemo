#version 330 core
layout (location = 0) in vec3 vCoordinate;
layout (location = 1) in vec3 vColor;

uniform mat4 cModel;
uniform mat4 cView;
uniform mat4 cPMatrix;

void main()
{
    gl_Position = cPMatrix * cView * cModel * vec4(vCoordinate, 1.0);
}
