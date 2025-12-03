using InlineMethod;

namespace KcpSharpN.Internal
{
    internal static unsafe class KcpEncode
    {
        //---------------------------------------------------------------------
        // encode / decode
        //---------------------------------------------------------------------

        /* encode 8 bits unsigned int */
        [Inline]
        static inline char* ikcp_encode8u(char* p, byte c)
        {
            *(unsigned char*)p++ = c;
            return p;
        }

        /* decode 8 bits unsigned int */
        static inline const char* ikcp_decode8u(const char* p, unsigned char* c)
{
	*c = *(unsigned char*)p++;
	return p;
}

    /* encode 16 bits unsigned int (lsb) */
    static inline char* ikcp_encode16u(char* p, unsigned short w)
    {
#if IWORDS_BIG_ENDIAN || IWORDS_MUST_ALIGN
	*(unsigned char*)(p + 0) = (w & 255);
	*(unsigned char*)(p + 1) = (w >> 8);
#else
        memcpy(p, &w, 2);
#endif
        p += 2;
        return p;
    }

    /* decode 16 bits unsigned int (lsb) */
    static inline const char* ikcp_decode16u(const char * p, unsigned short * w)
{
#if IWORDS_BIG_ENDIAN || IWORDS_MUST_ALIGN
	*w = *(const unsigned char*)(p + 1);
	*w = *(const unsigned char*)(p + 0) + (*w << 8);
#else
	memcpy(w, p, 2);
#endif
    p += 2;
	return p;
}

/* encode 32 bits unsigned int (lsb) */
static inline char* ikcp_encode32u(char* p, IUINT32 l)
{
#if IWORDS_BIG_ENDIAN || IWORDS_MUST_ALIGN
	*(unsigned char*)(p + 0) = (unsigned char)((l >>  0) & 0xff);
	*(unsigned char*)(p + 1) = (unsigned char)((l >>  8) & 0xff);
	*(unsigned char*)(p + 2) = (unsigned char)((l >> 16) & 0xff);
	*(unsigned char*)(p + 3) = (unsigned char)((l >> 24) & 0xff);
#else
    memcpy(p, &l, 4);
#endif
    p += 4;
    return p;
}

/* decode 32 bits unsigned int (lsb) */
static inline const char* ikcp_decode32u(const char * p, IUINT32 * l)
{
#if IWORDS_BIG_ENDIAN || IWORDS_MUST_ALIGN
	*l = *(const unsigned char*)(p + 3);
	*l = *(const unsigned char*)(p + 2) + (*l << 8);
	*l = *(const unsigned char*)(p + 1) + (*l << 8);
	*l = *(const unsigned char*)(p + 0) + (*l << 8);
#else
    memcpy(l, p, 4);
#endif
    p += 4;
    return p;
}

static inline IUINT32 _imin_(IUINT32 a, IUINT32 b) {
	return a <= b ? a : b;
}

static inline IUINT32 _imax_(IUINT32 a, IUINT32 b) {
	return a >= b ? a : b;
}

static inline IUINT32 _ibound_(IUINT32 lower, IUINT32 middle, IUINT32 upper) 
{
	return _imin_(_imax_(lower, middle), upper);
}

static inline long _itimediff(IUINT32 later, IUINT32 earlier)
{
    return ((IINT32)(later - earlier));
}
    }
}
