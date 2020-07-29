// 小数求和，避免精度丢失
function numAdd(numList) {
    var base = 0;
    for (let i = 0; i < numList.length; i++) {
        var numBase;
        try {
            numBase = numList[i].toString().split(".")[1].length;
        } catch (e) {
            numBase = 0;
        }
        if(base < numBase) {
            base = numBase;
        }
    }
    base = Math.pow(10, base);
    var sum = 0;
    for (let i = 0; i < numList.length; i++) {
        sum += numList[i] * base;
    }
    return sum / base;
}

// 小数求差，避免精度丢失
function numSub(minuend, subtrahend) {
    var baseNum = 0;
    var baseMinuend, baseSubtrahend;
    // 精度
    var precision;
    try {
        baseMinuend = minuend.toString().split(".")[1].length;
    } catch (e) {
        baseMinuend = 0;
    }
    try {
        baseSubtrahend = subtrahend.toString().split(".")[1].length;
    } catch (e) {
        baseSubtrahend = 0;
    }
    baseNum = Math.pow(10, Math.max(baseMinuend, baseSubtrahend));
    precision = (baseMinuend >= baseSubtrahend) ? baseMinuend : baseSubtrahend;
    return ((minuend * baseNum - subtrahend * baseNum) / baseNum).toFixed(precision);
};

// 小数求积，避免精度丢失
function numMulti(multiplierOne, multiplierTwo) {
    var baseNum = 0;
    try {
        baseNum += multiplierOne.toString().split(".")[1].length;
    } catch (e) {
    }
    try {
        baseNum += multiplierTwo.toString().split(".")[1].length;
    } catch (e) {
    }
    return Number(multiplierOne.toString().replace(".", "")) * Number(multiplierTwo.toString().replace(".", "")) / Math.pow(10, baseNum);
};

 // 小数求商，避免精度丢失
function numDiv(divisor, dividend) {
    var baseNum = 0;
    var baseDivisor, baseDividend;
    try {
        baseDivisor = divisor.toString().split(".")[1].length;
    } catch (e) {
        baseDivisor = 0;
    }
    try {
        baseDividend = dividend.toString().split(".")[1].length;
    } catch (e) {
        baseDividend = 0;
    }
    baseNum = baseDividend - baseDivisor;
    return (Number(divisor.toString().replace(".", "")) / Number(dividend.toString().replace(".", ""))) * Math.pow(10, baseNum);
};