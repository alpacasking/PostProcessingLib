
float3 AKRgbToLab (float3 rgb,float strength)
{
    float L = 0.3811*rgb.r + 0.5783*rgb.g + 0.0402*rgb.b;
    float M = 0.1967*rgb.r + 0.7244*rgb.g + 0.0782*rgb.b;
    float S = 0.0241*rgb.r + 0.1288*rgb.g + 0.8444*rgb.b;
    
    //若RGB值均为0，则LMS为0，防止数学错误log0
    if(L!=0) L = log(L)/log(10);
    if(M!=0) M = log(M)/log(10);
    if(S!=0) S = log(S)/log(10);
    
    float l = (L + M + S)/sqrt(3);
    float a = (L + M - 2*S)/sqrt(6);
    float b = (L - M)/sqrt(2);

    return float3(l*strength,a*strength,b*strength);
}

float3 AKLabToRgb (float3 lab,float strength)
{
    if(lab.x ==0){
        return float3(0,0,0);
    }
    float l = lab.x/strength/sqrt(3);
    float a = lab.y/strength/sqrt(6); 
    float b = lab.z/strength/sqrt(2);
    float L = l + a + b;
    float M = l + a - b;
    float S = l - 2*a;
    
    L = pow(10,L);
    M = pow(10,M);
    S = pow(10,S);
    
    float R = saturate(4.4679*L - 3.5873*M + 0.1193*S);
    float G = saturate(-1.2186*L + 2.3809*M - 0.1624*S);
    float B = saturate(0.0497*L - 0.2439*M + 1.2045*S);

    return float3(R,G,B);
}

float RgbToGrayScale (float3 rgb)
{
	float grayScale = rgb.r*0.299 + rgb.g*0.587 + rgb.b*0.114;
	return grayScale;
}

// return eigenvectors and eigenvalues
void StructureTensorToEigen(float3 structureTensor,out float2 majorEigenvector,out float2 minorEigenvector,out float majorEigenValue,out float minorEigenValue )
{
	float t1 = structureTensor.x + structureTensor.z;
	float t2 = sqrt(pow(structureTensor.x -  structureTensor.z,2)+4*pow(structureTensor.y,2));
	majorEigenValue = (t1 + t2)/2;
	minorEigenValue = (t1 - t2)/2;
	majorEigenvector = float2(structureTensor.y,majorEigenValue-structureTensor.x);
	minorEigenvector = float2(majorEigenValue-structureTensor.x,-structureTensor.y);
}

uint2 BoundIndex(uint2 i,uint2 bound){
	return uint2(clamp(i.x,0,bound.x),clamp(i.y,0,bound.y));
}
