// Fragment Shader : basic.frag
#version 460 core

in  vec3 FragPos;
in  vec3 Normal;
in  vec3 Color;

out vec4 FragColor;

uniform vec3  camPos;
uniform vec3  lightPos;
uniform vec3  lightColor;
uniform bool  LightSwitch;
uniform float metallic;
uniform float shininess;

const float PI = 3.14159265359;

void main()
{
    vec3 result;

    if (LightSwitch)
    {
        vec3 baseColor = vec3(0.58, 0.52, 0.63);

        vec3 N = normalize(Normal);
        vec3 V = normalize(camPos   - FragPos);
        vec3 L = normalize(lightPos - FragPos);
        vec3 H = normalize(V + L);

        if (dot(N, L) < 0.0)
            N = -N;

        float dist       = length(lightPos - FragPos);
        float attenuation = 1.0 / (dist * dist + 0.1);
        vec3  radiance    = lightColor * attenuation;

        vec3 ambient  = 0.9 * baseColor;

        float cosT    = max(dot(N, L), 0.0);
        vec3 diffuse  = baseColor * radiance * cosT / (PI * 0.5);

        float specPow = pow(max(dot(N, H), 0.0), shininess);
        vec3 specular = lightColor * specPow * metallic;

        result = ambient + diffuse + specular;
    }
    else
    {
        result = Color;
    }

    FragColor = vec4(result, 1.0);
}
